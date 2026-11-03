using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Distributed;
using Noovio.FrontPulse.Application.Abstractions;
using Noovio.FrontPulse.Domain.Entities;
using Noovio.FrontPulse.Domain.Enums;
using Noovio.FrontPulse.Infrastructure.RaiderIo;

namespace Noovio.FrontPulse.Infrastructure.Blizzard;

/// <summary>
/// Traduit les profils Blizzard (équipement, spécialisations, caractéristiques) en modèle FrontPulse :
/// profils du top pour l'ingestion, et fiche complète (avec Raider.io) pour l'analyse d'un personnage.
/// </summary>
public sealed class BlizzardCharacterSource(BlizzardClient client, RaiderIoClient raiderIo) : ICharacterLoadoutSource, ICharacterDetailsSource
{
    public async Task<CharacterLoadout?> GetLoadoutAsync(LeaderboardTank tank, CancellationToken ct)
    {
        var equipmentTask = client.GetEquipmentAsync(tank.Region, tank.RealmSlug, tank.Name, ct);
        var specsTask = client.GetSpecializationsAsync(tank.Region, tank.RealmSlug, tank.Name, ct);
        var statsTask = client.GetStatisticsAsync(tank.Region, tank.RealmSlug, tank.Name, ct);
        await Task.WhenAll(equipmentTask, specsTask, statsTask);

        return await equipmentTask is { } equipment
            ? BuildLoadout(equipment, await specsTask, await statsTask, talentsOf: null)
            : null;
    }

    public async Task<CharacterDetails?> GetAsync(string region, string realmSlug, string name, TankSpec tankSpec, CancellationToken ct)
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
            BuildLoadout(equipment, specs, stats, talentsOf: (int)tankSpec),
            ToSheet(stats),
            media?.Assets.FirstOrDefault(a => a.Key == "inset")?.Value ?? media?.Assets.FirstOrDefault(a => a.Key == "main-raw")?.Value,
            media?.Assets.FirstOrDefault(a => a.Key == "avatar")?.Value,
            await rioTask);
    }

    /// <summary>
    /// Talents de <paramref name="talentsOf"/> si précisé (configuration active de cette spécialisation,
    /// même si le personnage en joue une autre), sinon de la spécialisation active.
    /// </summary>
    private static CharacterLoadout BuildLoadout(BzEquipment equipment, BzSpecializations? specs, BzStatistics? stats, int? talentsOf)
    {
        var gear = equipment.EquippedItems.Select(ToGear).OfType<GearPiece>().ToList();
        var specId = talentsOf ?? specs?.ActiveSpecialization?.Id;
        var entry = specs?.Specializations.FirstOrDefault(s => s.Specialization.Id == specId);
        var loadout = entry?.Loadouts.FirstOrDefault(l => l.IsActive) ?? entry?.Loadouts.FirstOrDefault();
        var heroTree = loadout?.SelectedHeroTalentTree?.Name
                       ?? (specId == specs?.ActiveSpecialization?.Id ? specs?.ActiveHeroTalentTree?.Name : null);

        return new CharacterLoadout(
            specs?.ActiveSpecialization?.Id,
            ItemLevel(gear),
            gear,
            ToStats(stats),
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
                season?.Segments.GetValueOrDefault("tank")?.Score ?? 0,
                rio.ProfileUrl,
                rio.MythicPlusRanks?.Tank?.World,
                rio.MythicPlusRanks?.ClassTank?.World,
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

    private static CharacterSheet? ToSheet(BzStatistics? s) => s is null || ToStats(s) is not { } secondary
        ? null
        : new CharacterSheet(
            s.Health,
            s.Stamina?.Effective ?? 0,
            s.Strength?.Effective ?? 0,
            s.Agility?.Effective ?? 0,
            s.Armor?.Effective ?? 0,
            secondary,
            s.VersatilityDamageTakenBonus,
            s.Dodge?.Value ?? 0,
            s.Parry?.Value ?? 0,
            s.Block?.Value ?? 0,
            s.Lifesteal?.Value ?? 0,
            s.Avoidance?.Value ?? 0,
            s.Speed?.Value ?? 0);

    private static GearPiece? ToGear(BzEquippedItem item)
    {
        GearSlot? slot = item.Slot.Type switch
        {
            "HEAD" => GearSlot.Head,
            "NECK" => GearSlot.Neck,
            "SHOULDER" => GearSlot.Shoulder,
            "BACK" => GearSlot.Back,
            "CHEST" => GearSlot.Chest,
            "WRIST" => GearSlot.Wrist,
            "HANDS" => GearSlot.Hands,
            "WAIST" => GearSlot.Waist,
            "LEGS" => GearSlot.Legs,
            "FEET" => GearSlot.Feet,
            "FINGER_1" or "FINGER_2" => GearSlot.Finger,
            "TRINKET_1" or "TRINKET_2" => GearSlot.Trinket,
            "MAIN_HAND" => GearSlot.MainHand,
            "OFF_HAND" => GearSlot.OffHand,
            _ => null // chemise, tabard
        };

        return slot is null
            ? null
            : new GearPiece(
                slot.Value,
                item.Item.Id,
                item.Name,
                item.Level?.Value ?? 0,
                item.BonusList,
                item.Enchantments.Select(e => e.DisplayString).OfType<string>().ToList(),
                item.Spells.Select(s => new ItemEffect(s.Spell.Id, s.Spell.Name ?? "", s.Description)).ToList(),
                IsEmbellished(item.LimitCategory));
    }

    /// <summary>« Unique-Equipped: Embellished (2) » en anglais, « Unique - Équipé : Enjolivement (2) » en français.</summary>
    private static bool IsEmbellished(string? limitCategory) =>
        limitCategory is not null
        && (limitCategory.Contains("Embellished", StringComparison.OrdinalIgnoreCase)
            || limitCategory.Contains("Enjolivement", StringComparison.OrdinalIgnoreCase));

    /// <summary>Comme le jeu : 16 emplacements, une arme à deux mains compte deux fois.</summary>
    private static double ItemLevel(IReadOnlyList<GearPiece> gear)
    {
        var total = gear.Sum(g => g.ItemLevel);
        if (gear.FirstOrDefault(g => g.Slot == GearSlot.MainHand) is { } mainHand && gear.All(g => g.Slot != GearSlot.OffHand))
        {
            total += mainHand.ItemLevel;
        }

        return Math.Round(total / 16.0, 1);
    }

    private static StatSheet? ToStats(BzStatistics? stats) => stats is null
        ? null
        : new StatSheet(
            Haste: new(stats.MeleeHaste?.Rating ?? 0, stats.MeleeHaste?.Value ?? 0),
            Versatility: new(stats.Versatility, stats.VersatilityDamageDoneBonus),
            Mastery: new(stats.Mastery?.Rating ?? 0, stats.Mastery?.Value ?? 0),
            Crit: new(stats.MeleeCrit?.Rating ?? 0, stats.MeleeCrit?.Value ?? 0));

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
