using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Distributed;
using Noovio.Pulse.Application.Abstractions;
using Noovio.Pulse.Domain;
using Noovio.Pulse.Domain.Entities;
using Noovio.Pulse.Domain.Enums;
using Noovio.Pulse.Domain.Roles;
using Noovio.Pulse.Infrastructure.RaiderIo;

namespace Noovio.Pulse.Infrastructure.Blizzard;

/// <summary>
/// Traduit les profils Blizzard (équipement, spécialisations, caractéristiques) en modèle Pulse :
/// profils du top pour l'ingestion, et fiche complète (avec Raider.io) pour l'analyse d'un personnage.
/// </summary>
public sealed class BlizzardCharacterSource(BlizzardClient client, RaiderIoClient raiderIo, PulseRole role)
    : ICharacterLoadoutSource, ICharacterDetailsSource, ICharacterExtrasSource
{
    public async Task<CharacterExtras> GetExtrasAsync(string region, string realmSlug, string name, CancellationToken ct)
    {
        var mediaTask = GetMediaAsync(region, realmSlug, name, ct);
        var rioTask = GetMythicPlusAsync(region, realmSlug, name, ct);
        await Task.WhenAll(mediaTask, rioTask);

        var media = await mediaTask;
        return new CharacterExtras(Portrait(media), Avatar(media), await rioTask);
    }

    /// <summary>Portrait facultatif : Blizzard indisponible ou personnage inconnu (import d'un personnage jamais déconnecté), l'analyse s'en passe.</summary>
    private async Task<BzCharacterMedia?> GetMediaAsync(string region, string realmSlug, string name, CancellationToken ct)
    {
        try
        {
            return await client.GetCharacterMediaAsync(region, realmSlug, name, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            return null;
        }
    }

    private static string? Portrait(BzCharacterMedia? media) =>
        media?.Assets.FirstOrDefault(a => a.Key == "inset")?.Value ?? media?.Assets.FirstOrDefault(a => a.Key == "main-raw")?.Value;

    private static string? Avatar(BzCharacterMedia? media) => media?.Assets.FirstOrDefault(a => a.Key == "avatar")?.Value;

    public async Task<CharacterLoadout?> GetLoadoutAsync(LeaderboardPlayer player, CancellationToken ct)
    {
        var equipmentTask = client.GetEquipmentAsync(player.Region, player.RealmSlug, player.Name, ct);
        var specsTask = client.GetSpecializationsAsync(player.Region, player.RealmSlug, player.Name, ct);
        var statsTask = client.GetStatisticsAsync(player.Region, player.RealmSlug, player.Name, ct);
        await Task.WhenAll(equipmentTask, specsTask, statsTask);

        // L'ingestion ne garde le profil que si la spécialisation active est celle du classement : ses lignes de la feuille font foi.
        return await equipmentTask is { } equipment
            ? BuildLoadout(equipment, await specsTask, await statsTask, talentsOf: null, player.Spec)
            : null;
    }

    public async Task<CharacterDetails?> GetAsync(string region, string realmSlug, string name, WowSpec spec, CancellationToken ct)
    {
        var equipmentTask = client.GetEquipmentAsync(region, realmSlug, name, ct);
        var specsTask = client.GetSpecializationsAsync(region, realmSlug, name, ct);
        var statsTask = client.GetStatisticsAsync(region, realmSlug, name, ct);
        var mediaTask = client.GetCharacterMediaAsync(region, realmSlug, name, ct);
        var rioTask = GetMythicPlusAsync(region, realmSlug, name, ct);
        await Task.WhenAll(equipmentTask, specsTask, statsTask, mediaTask, rioTask);

        if (await equipmentTask is not { } equipment)
        {
            return null;
        }

        var specs = await specsTask;
        var stats = await statsTask;
        var media = await mediaTask;
        var activeName = specs?.Specializations.FirstOrDefault(s => s.Specialization.Id == specs.ActiveSpecialization?.Id)?.Specialization.Name
                         ?? specs?.ActiveSpecialization?.Name;

        return new CharacterDetails(
            name,
            region,
            realmSlug,
            specs?.ActiveSpecialization?.Id,
            activeName,
            BuildLoadout(equipment, specs, stats, talentsOf: (int)spec, spec),
            ToSheet(stats, spec),
            Portrait(media),
            Avatar(media),
            await rioTask);
    }

    /// <summary>
    /// Talents de <paramref name="talentsOf"/> si précisé (configuration active de cette spécialisation,
    /// même si le personnage en joue une autre), sinon de la spécialisation active.
    /// Caractéristiques lues comme les lit <paramref name="statsOf"/>.
    /// </summary>
    private CharacterLoadout BuildLoadout(BzEquipment equipment, BzSpecializations? specs, BzStatistics? stats, int? talentsOf, WowSpec statsOf)
    {
        var gear = equipment.EquippedItems.Select(ToGear).OfType<GearPiece>().ToList();
        var specId = talentsOf ?? specs?.ActiveSpecialization?.Id;
        var entry = specs?.Specializations.FirstOrDefault(s => s.Specialization.Id == specId);
        var loadout = entry?.Loadouts.FirstOrDefault(l => l.IsActive) ?? entry?.Loadouts.FirstOrDefault();
        var heroTree = loadout?.SelectedHeroTalentTree?.Name
                       ?? (specId == specs?.ActiveSpecialization?.Id ? specs?.ActiveHeroTalentTree?.Name : null);

        return new CharacterLoadout(
            specs?.ActiveSpecialization?.Id,
            GearRules.AverageItemLevel(gear),
            gear,
            ToStats(stats, statsOf),
            loadout is null ? [] : Talents(loadout),
            loadout?.TalentLoadoutCode,
            heroTree);
    }

    private async Task<MythicPlusProfile?> GetMythicPlusAsync(string region, string realmSlug, string name, CancellationToken ct)
    {
        try
        {
            if (await raiderIo.GetCharacterAsync(region, realmSlug, name, ct) is not { } rio)
            {
                return null;
            }

            var season = rio.MythicPlusScoresBySeason.FirstOrDefault();
            var all = season?.Segments.GetValueOrDefault("all");
            var dungeons = (await raiderIo.GetMythicPlusStaticDataAsync(ct))?.Seasons.FirstOrDefault(s => s.Slug == season?.Season)?.Dungeons ?? [];

            return new MythicPlusProfile(
                all?.Score ?? 0,
                all?.Color,
                season?.Segments.GetValueOrDefault(role.RaiderIoRole)?.Score ?? 0,
                rio.ProfileUrl,
                RoleRank(rio.MythicPlusRanks, classRank: false)?.World,
                RoleRank(rio.MythicPlusRanks, classRank: true)?.World,
                rio.MythicPlusBestRuns
                    .OrderByDescending(r => r.Score)
                    .Select(r => new MythicPlusRun(r.Dungeon, r.ShortName, r.MythicLevel, r.NumKeystoneUpgrades, r.Score, r.Url))
                    .ToList(),
                dungeons.Where(d => rio.MythicPlusBestRuns.All(r => r.Dungeon != d.Name)).Select(d => d.Name).ToList());
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            // Raider.io indisponible : l'analyse se fait sans la partie M+.
            return null;
        }
    }

    private RioRank? RoleRank(RioRanks? ranks, bool classRank) => role.Kind switch
    {
        RoleKind.Tank => classRank ? ranks?.ClassTank : ranks?.Tank,
        RoleKind.Healer => classRank ? ranks?.ClassHealer : ranks?.Healer,
        _ => classRank ? ranks?.ClassDps : ranks?.Dps
    };

    private CharacterSheet? ToSheet(BzStatistics? s, WowSpec spec) => s is null || ToStats(s, spec) is not { } secondary
        ? null
        : new CharacterSheet(
            s.Health,
            s.Stamina?.Effective ?? 0,
            s.Strength?.Effective ?? 0,
            s.Agility?.Effective ?? 0,
            s.Intellect?.Effective ?? 0,
            role.Kind == RoleKind.Healer ? s.Power : 0,
            s.Armor?.Effective ?? 0,
            secondary,
            s.VersatilityDamageTakenBonus,
            s.Dodge?.Value ?? 0,
            s.Parry?.Value ?? 0,
            s.Block?.Value ?? 0,
            s.Lifesteal?.Value ?? 0,
            s.Avoidance?.Value ?? 0,
            s.Speed?.Value ?? 0);

    internal static GearPiece? ToGear(BzEquippedItem item)
    {
        var slot = GearRules.SlotOf(item.Slot.Type);

        return slot is null
            ? null
            : new GearPiece(
                slot.Value,
                item.Item.Id,
                item.Name,
                item.Level?.Value ?? 0,
                item.BonusList,
                item.Enchantments.Where(IsPermanent).Select(e => e.DisplayString).OfType<string>().ToList(),
                item.Spells.Select(s => new ItemEffect(s.Spell.Id, s.Spell.Name ?? "", s.Description)).ToList(),
                GearRules.IsEmbellished(item.LimitCategory),
                item.Sockets.Select(s => new GemSocket(s.SocketType?.Type, s.Item?.Id, s.Item?.Name, s.DisplayString)).ToList(),
                item.Stats.Where(s => !s.IsNegated).Select(ToItemStat).OfType<ItemStat>().ToList());
    }

    /// <summary>
    /// Les huiles et imprégnations (chaman : Garde de mande-flots sur le bouclier) sont aussi listées, en TEMPORARY :
    /// ce ne sont pas des enchantements à poser. Sans emplacement indiqué, l'entrée est gardée.
    /// </summary>
    private static bool IsPermanent(BzEnchantment enchantment) =>
        enchantment.EnchantmentSlot?.Type is not { } type || type.Equals("PERMANENT", StringComparison.OrdinalIgnoreCase);

    /// <summary>Types attendus : CRIT_RATING, HASTE_RATING, MASTERY_RATING, VERSATILITY ; l'indice primaire et l'endurance sont ignorés.</summary>
    private static ItemStat? ToItemStat(BzItemStat stat)
    {
        var type = stat.Type?.Type ?? "";
        SecondaryStat? secondary = type.Contains("HASTE", StringComparison.OrdinalIgnoreCase) ? SecondaryStat.Haste
            : type.Contains("VERSATILITY", StringComparison.OrdinalIgnoreCase) ? SecondaryStat.Versatility
            : type.Contains("MASTERY", StringComparison.OrdinalIgnoreCase) ? SecondaryStat.Mastery
            : type.Contains("CRIT", StringComparison.OrdinalIgnoreCase) ? SecondaryStat.Crit
            : null;
        return secondary is null ? null : new ItemStat(secondary.Value, stat.Value);
    }

    private StatSheet? ToStats(BzStatistics? stats, WowSpec spec) =>
        stats is null ? null : ToStats(stats, role.BasisOf(spec), role.Kind == RoleKind.Healer);

    /// <summary>
    /// Chaque spécialisation se lit sur les lignes de sa façon de combattre (sort, distance, mêlée) : les autres n'ont pas
    /// ses bonus et ne correspondent pas à la feuille en jeu. La polyvalence est celle des soins pour un soigneur, des dégâts sinon.
    /// </summary>
    internal static StatSheet ToStats(BzStatistics stats, StatBasis basis, bool healing)
    {
        var (crit, haste) = basis switch
        {
            StatBasis.Spell => (stats.SpellCrit, stats.SpellHaste),
            StatBasis.Ranged => (stats.RangedCrit, stats.RangedHaste),
            _ => ((BzRatingStat?)null, (BzRatingStat?)null)
        };

        return new StatSheet(
            Haste: Line(haste, stats.MeleeHaste),
            Versatility: new(stats.Versatility, healing ? stats.VersatilityHealingDoneBonus : stats.VersatilityDamageDoneBonus),
            Mastery: new(stats.Mastery?.Rating ?? 0, stats.Mastery?.Value ?? 0),
            Crit: Line(crit, stats.MeleeCrit));

        // L'indice est le même sur toutes les lignes (seul le pourcentage diffère) : celui de mêlée sert de repli.
        static StatValue Line(BzRatingStat? preferred, BzRatingStat? melee) => new(
            preferred?.Rating is > 0 ? preferred.Rating : melee?.Rating ?? 0,
            preferred?.Value ?? melee?.Value ?? 0);
    }

    private static List<TalentPick> Talents(BzLoadout loadout) =>
        loadout.SelectedClassTalents.Select(t => ToPick(TalentTree.Class, t))
            .Concat(loadout.SelectedSpecTalents.Select(t => ToPick(TalentTree.Spec, t)))
            .Concat(loadout.SelectedHeroTalents.Select(t => ToPick(TalentTree.Hero, t)))
            .ToList();

    private static TalentPick ToPick(TalentTree tree, BzSelectedTalent t) => new(
        tree,
        t.Tooltip?.Talent?.Id ?? t.Id,
        t.Tooltip?.SpellTooltip?.Spell?.Id ?? 0,
        t.Tooltip?.Talent?.Name ?? t.Tooltip?.SpellTooltip?.Spell?.Name ?? $"#{t.Id}",
        Math.Max(1, t.Rank));
}

/// <summary>Icônes d'objets et de sorts : immuables, donc gardées 30 jours en cache distribué.</summary>
public sealed class BlizzardMediaSource(BlizzardClient client, IDistributedCache cache) : IMediaSource
{
    private static readonly DistributedCacheEntryOptions CacheOptions = new() { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(30) };

    public Task<IReadOnlyDictionary<int, string>> GetItemIconsAsync(IEnumerable<int> itemIds, CancellationToken ct) =>
        GetIconsAsync("item-icon", itemIds, client.GetItemMediaAsync, ct);

    public Task<IReadOnlyDictionary<int, string>> GetSpellIconsAsync(IEnumerable<int> spellIds, CancellationToken ct) =>
        GetIconsAsync("spell-icon", spellIds, client.GetSpellMediaAsync, ct);

    private async Task<IReadOnlyDictionary<int, string>> GetIconsAsync(
        string prefix, IEnumerable<int> ids, Func<int, CancellationToken, Task<BzMedia?>> fetch, CancellationToken ct)
    {
        var icons = new ConcurrentDictionary<int, string>();
        var parallel = new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = ct };

        await Parallel.ForEachAsync(ids.Distinct(), parallel, async (id, token) =>
        {
            var key = $"{prefix}:{id}";
            var url = await cache.GetStringAsync(key, token);
            if (url is null)
            {
                var media = await fetch(id, token);
                url = media?.Assets.FirstOrDefault(a => a.Key == "icon")?.Value;
                if (url is null)
                {
                    return;
                }

                await cache.SetStringAsync(key, url, CacheOptions, token);
            }

            icons[id] = url;
        });

        return icons;
    }
}
