using System.Globalization;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using AltPulse.Client.Models;
using AltPulse.Client.Services;
using AltPulse.Services.Blizzard;
using AltPulse.Services.RaiderIo;

namespace AltPulse.Services;

/// <summary>
/// Implémentation serveur : combine Raider.io (M+, score, progression, coffre) et Blizzard
/// (profil, équipement, médias, verrouillages de raid de la semaine).
/// </summary>
public sealed partial class AltPulseService(
    RaiderIoClient raiderIo,
    BlizzardClient blizzard,
    IMemoryCache cache,
    ILogger<AltPulseService> logger) : IAltPulseApi
{
    private static readonly TimeSpan SeasonCacheDuration = TimeSpan.FromHours(1);
    private static readonly TimeSpan CharacterCacheDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan RealmCacheDuration = TimeSpan.FromHours(24);

    private static readonly int[] DungeonVaultThresholds = [1, 4, 8];
    private static readonly int[] RaidVaultThresholds = [2, 4, 6];

    private static readonly string[] SlotOrder =
    [
        "HEAD", "NECK", "SHOULDER", "BACK", "CHEST", "SHIRT", "TABARD", "WRIST",
        "HANDS", "WAIST", "LEGS", "FEET", "FINGER_1", "FINGER_2", "TRINKET_1", "TRINKET_2",
        "MAIN_HAND", "OFF_HAND"
    ];

    private static readonly Dictionary<string, string> RioSlotToBlizzard = new(StringComparer.OrdinalIgnoreCase)
    {
        ["head"] = "HEAD", ["neck"] = "NECK", ["shoulder"] = "SHOULDER", ["back"] = "BACK",
        ["chest"] = "CHEST", ["shirt"] = "SHIRT", ["tabard"] = "TABARD", ["wrist"] = "WRIST",
        ["hands"] = "HANDS", ["waist"] = "WAIST", ["legs"] = "LEGS", ["feet"] = "FEET",
        ["finger1"] = "FINGER_1", ["finger2"] = "FINGER_2", ["trinket1"] = "TRINKET_1",
        ["trinket2"] = "TRINKET_2", ["mainhand"] = "MAIN_HAND", ["offhand"] = "OFF_HAND"
    };

    private static readonly Dictionary<string, string> SlotNames = new()
    {
        ["HEAD"] = "Tête", ["NECK"] = "Cou", ["SHOULDER"] = "Épaules", ["BACK"] = "Dos",
        ["CHEST"] = "Torse", ["SHIRT"] = "Chemise", ["TABARD"] = "Tabard", ["WRIST"] = "Poignets",
        ["HANDS"] = "Mains", ["WAIST"] = "Taille", ["LEGS"] = "Jambes", ["FEET"] = "Pieds",
        ["FINGER_1"] = "Doigt 1", ["FINGER_2"] = "Doigt 2", ["TRINKET_1"] = "Bijou 1",
        ["TRINKET_2"] = "Bijou 2", ["MAIN_HAND"] = "Main droite", ["OFF_HAND"] = "Main gauche"
    };

    private static readonly Dictionary<string, int> ClassIds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Warrior"] = 1, ["Paladin"] = 2, ["Hunter"] = 3, ["Rogue"] = 4, ["Priest"] = 5,
        ["Death Knight"] = 6, ["Shaman"] = 7, ["Mage"] = 8, ["Warlock"] = 9, ["Monk"] = 10,
        ["Druid"] = 11, ["Demon Hunter"] = 12, ["Evoker"] = 13
    };

    private static readonly string[] Qualities = ["POOR", "COMMON", "UNCOMMON", "RARE", "EPIC", "LEGENDARY", "ARTIFACT", "HEIRLOOM"];

    public async Task<SeasonInfo?> GetSeasonAsync(string region, CancellationToken cancellationToken = default)
    {
        region = NormalizeRegion(region);
        return await cache.GetOrCreateAsync($"season:{region}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = SeasonCacheDuration;

            var mythicPlusTask = raiderIo.GetMythicPlusStaticDataAsync(cancellationToken);
            var raidsTask = raiderIo.GetRaidStaticDataAsync(cancellationToken);
            var affixesTask = SafeAsync(() => raiderIo.GetAffixesAsync(region, cancellationToken), "affixes");
            await Task.WhenAll(mythicPlusTask, raidsTask, affixesTask);

            var now = DateTimeOffset.UtcNow;
            var seasons = mythicPlusTask.Result?.Seasons ?? [];
            var season = seasons.FirstOrDefault(s => s.IsMainSeason && IsActive(s.Starts, s.Ends, region, now))
                         ?? seasons.FirstOrDefault();
            if (season is null)
            {
                return null;
            }

            var (currentReset, nextReset) = ComputeWeeklyReset(season.Starts.For(region), region, now);

            var raids = (raidsTask.Result?.Raids ?? [])
                .Where(r => IsActive(r.Starts, r.Ends, region, now))
                .OrderByDescending(r => r.Encounters.Count)
                .Select(r => new RaidInfo(
                    r.Slug,
                    r.Name,
                    r.ShortName,
                    r.Encounters.Select(e => new EncounterInfo(e.Slug, e.Name)).ToList()))
                .ToList();

            var affixes = affixesTask.Result is { } a
                ? new AffixWeek(a.Title, a.AffixDetails.Select(x => new AffixInfo(x.Id, x.Name, x.Description, x.IconUrl, x.WowheadUrl)).ToList())
                : null;

            return new SeasonInfo(
                region,
                season.Slug,
                season.Name,
                currentReset,
                nextReset,
                season.Dungeons
                    .OrderBy(d => d.Name)
                    .Select(d => new DungeonInfo(d.Slug, d.Name, d.ShortName, d.ChallengeModeId, d.KeystoneTimerSeconds, d.IconUrl, d.BackgroundImageUrl))
                    .ToList(),
                raids,
                affixes);
        });
    }

    public async Task<IReadOnlyList<RealmInfo>> GetRealmsAsync(string region, CancellationToken cancellationToken = default)
    {
        region = NormalizeRegion(region);
        if (!blizzard.IsConfigured)
        {
            return [];
        }

        return await cache.GetOrCreateAsync($"realms:{region}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = RealmCacheDuration;
            var index = await SafeAsync(() => blizzard.GetRealmsAsync(region, cancellationToken), "realms");
            return (IReadOnlyList<RealmInfo>)(index?.Realms ?? [])
                .Where(r => !string.IsNullOrWhiteSpace(r.Slug))
                .Select(r => new RealmInfo(r.Slug!, r.Name ?? r.Slug!))
                .OrderBy(r => r.Name, StringComparer.Create(CultureInfo.GetCultureInfo("fr-FR"), true))
                .ToList();
        }) ?? [];
    }

    public async Task<CharacterLookupResult> GetCharacterAsync(string region, string realm, string name, bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        region = NormalizeRegion(region);
        var realmSlug = Slugify(realm);
        name = name.Trim();

        if (string.IsNullOrWhiteSpace(realmSlug) || string.IsNullOrWhiteSpace(name))
        {
            return new CharacterLookupResult(null, "Royaume et nom du personnage sont obligatoires.");
        }

        var cacheKey = $"character:{CharacterKey.Of(region, realmSlug, name)}";
        if (!forceRefresh && cache.TryGetValue(cacheKey, out CharacterSnapshot? cached) && cached is not null)
        {
            return new CharacterLookupResult(cached, null);
        }

        var warnings = new List<string>();
        var seasonTask = SafeAsync(() => GetSeasonAsync(region, cancellationToken), "saison raider.io", warnings);
        var rioTask = SafeAsync(() => raiderIo.GetCharacterAsync(region, realmSlug, name, cancellationToken), "raider.io", warnings);
        var profileTask = SafeAsync(() => blizzard.GetProfileAsync(region, realmSlug, name, cancellationToken), "profil Blizzard", warnings);
        var equipmentTask = SafeAsync(() => blizzard.GetEquipmentAsync(region, realmSlug, name, cancellationToken), "équipement Blizzard", warnings);
        var mediaTask = SafeAsync(() => blizzard.GetMediaAsync(region, realmSlug, name, cancellationToken), "médias Blizzard", warnings);
        var raidsTask = SafeAsync(() => blizzard.GetRaidEncountersAsync(region, realmSlug, name, cancellationToken), "raids Blizzard", warnings);
        await Task.WhenAll(seasonTask, rioTask, profileTask, equipmentTask, mediaTask, raidsTask);

        var season = seasonTask.Result;
        var rio = rioTask.Result;
        var profile = profileTask.Result;

        if (rio is null && profile is null)
        {
            return new CharacterLookupResult(null, $"Personnage « {name} » introuvable sur {realm} ({region.ToUpperInvariant()}).");
        }

        if (!blizzard.IsConfigured)
        {
            warnings.Add("API Blizzard non configurée : verrouillages de raid de la semaine, avatar HD et équipement détaillé indisponibles.");
        }

        var media = mediaTask.Result?.Assets.ToDictionary(a => a.Key, a => a.Value, StringComparer.OrdinalIgnoreCase) ?? [];
        var finalRealmSlug = profile?.Realm?.Slug ?? realmSlug;
        var finalName = profile?.Name ?? rio?.Name ?? name;
        var score = rio?.MythicPlusScoresBySeason.FirstOrDefault()?.Segments.GetValueOrDefault("all");
        var raidKills = BuildRaidKillIndex(raidsTask.Result);

        var snapshot = new CharacterSnapshot
        {
            Region = region,
            RealmSlug = finalRealmSlug,
            RealmName = profile?.Realm?.Name ?? rio?.Realm ?? realm,
            Name = finalName,
            Level = profile?.Level ?? 0,
            ClassId = profile?.CharacterClass?.Id ?? ClassIds.GetValueOrDefault(rio?.Class ?? ""),
            ClassName = profile?.CharacterClass?.Name ?? rio?.Class,
            SpecName = profile?.ActiveSpec?.Name ?? rio?.ActiveSpecName,
            Role = rio?.ActiveSpecRole,
            RaceName = profile?.Race?.Name ?? rio?.Race,
            Faction = profile?.Faction?.Type?.ToLowerInvariant() ?? rio?.Faction,
            GuildName = profile?.Guild?.Name ?? rio?.Guild?.Name,
            AvatarUrl = media.GetValueOrDefault("avatar") ?? rio?.ThumbnailUrl,
            InsetUrl = media.GetValueOrDefault("inset"),
            RaiderIoUrl = rio?.ProfileUrl ?? $"https://raider.io/characters/{region}/{finalRealmSlug}/{Uri.EscapeDataString(finalName)}",
            ArmoryUrl = $"https://worldofwarcraft.blizzard.com/fr-fr/character/{region}/{finalRealmSlug}/{Uri.EscapeDataString(finalName.ToLowerInvariant())}",
            ItemLevelEquipped = profile?.EquippedItemLevel > 0 ? profile.EquippedItemLevel : rio?.Gear?.ItemLevelEquipped,
            ItemLevelAverage = profile?.AverageItemLevel > 0 ? profile.AverageItemLevel : null,
            Rating = score?.Score ?? 0,
            RatingColor = score?.Color,
            DungeonBests = BuildDungeonBests(rio, season),
            WeeklyRuns = MapWeeklyRuns(rio?.MythicPlusWeeklyHighestLevelRuns),
            PreviousWeeklyRuns = MapWeeklyRuns(rio?.MythicPlusPreviousWeeklyHighestLevelRuns),
            Vault = new VaultProgress(
                BuildDungeonVault(rio?.MythicPlusWeeklyHighestLevelRuns),
                BuildRaidVault(season, raidKills),
                BuildDungeonVault(rio?.MythicPlusPreviousWeeklyHighestLevelRuns)),
            Raids = BuildRaidLockouts(season, rio, raidKills),
            Equipment = BuildEquipment(equipmentTask.Result, rio?.Gear),
            FetchedAtUtc = DateTimeOffset.UtcNow,
            Warnings = warnings
        };

        cache.Set(cacheKey, snapshot, CharacterCacheDuration);
        return new CharacterLookupResult(snapshot, null);
    }

    private static List<DungeonBest> BuildDungeonBests(RioCharacterProfile? rio, SeasonInfo? season)
    {
        if (rio is null)
        {
            return [];
        }

        var slugByChallengeMode = season?.Dungeons.ToDictionary(d => d.ChallengeModeId, d => d.Slug) ?? [];
        return rio.MythicPlusBestRuns
            .Select(r => new DungeonBest(
                slugByChallengeMode.GetValueOrDefault(r.MapChallengeModeId) ?? Slugify(r.Dungeon),
                r.ShortName,
                r.MythicLevel,
                r.NumKeystoneUpgrades,
                r.Score,
                r.ClearTimeMs,
                r.ParTimeMs,
                r.Url))
            .ToList();
    }

    private static List<WeeklyRun> MapWeeklyRuns(List<RioRun>? runs) =>
        (runs ?? [])
        .OrderByDescending(r => r.MythicLevel)
        .Select(r => new WeeklyRun(r.ShortName, r.Dungeon, r.MythicLevel, r.NumKeystoneUpgrades, r.CompletedAt))
        .ToList();

    private static List<VaultSlot> BuildDungeonVault(List<RioRun>? runs)
    {
        var levels = (runs ?? []).Select(r => r.MythicLevel).OrderByDescending(l => l).ToList();
        return DungeonVaultThresholds
            .Select(t => new VaultSlot(t, Math.Min(levels.Count, t), levels.Count >= t ? $"+{levels[t - 1]}" : null))
            .ToList();
    }

    /// <summary>(nom de boss normalisé, difficulté) → dernier kill.</summary>
    private static Dictionary<(string Boss, RaidDifficulty Difficulty), DateTimeOffset> BuildRaidKillIndex(BzRaidEncounters? encounters)
    {
        var index = new Dictionary<(string, RaidDifficulty), DateTimeOffset>();
        if (encounters is null)
        {
            return index;
        }

        foreach (var mode in encounters.Expansions.SelectMany(e => e.Instances).SelectMany(i => i.Modes))
        {
            if (ParseDifficulty(mode.Difficulty.Type) is not { } difficulty)
            {
                continue;
            }

            foreach (var encounter in mode.Progress.Encounters.Where(e => e.LastKillTimestamp > 0))
            {
                var key = (NormalizeName(encounter.Encounter.Name), difficulty);
                var kill = DateTimeOffset.FromUnixTimeMilliseconds(encounter.LastKillTimestamp);
                if (!index.TryGetValue(key, out var existing) || kill > existing)
                {
                    index[key] = kill;
                }
            }
        }

        return index;
    }

    private static List<VaultSlot> BuildRaidVault(SeasonInfo? season, Dictionary<(string Boss, RaidDifficulty Difficulty), DateTimeOffset> kills)
    {
        var bestPerBoss = new List<RaidDifficulty>();
        if (season is not null)
        {
            foreach (var encounter in season.Raids.SelectMany(r => r.Encounters))
            {
                var boss = NormalizeName(encounter.Name);
                var best = Enum.GetValues<RaidDifficulty>()
                    .Where(d => kills.TryGetValue((boss, d), out var at) && at >= season.CurrentResetUtc)
                    .Select(d => (RaidDifficulty?)d)
                    .Max();
                if (best is { } difficulty)
                {
                    bestPerBoss.Add(difficulty);
                }
            }
        }

        bestPerBoss.Sort((a, b) => b.CompareTo(a));
        return RaidVaultThresholds
            .Select(t => new VaultSlot(t, Math.Min(bestPerBoss.Count, t), bestPerBoss.Count >= t ? DifficultyLabel(bestPerBoss[t - 1]) : null))
            .ToList();
    }

    private static List<RaidLockout> BuildRaidLockouts(
        SeasonInfo? season,
        RioCharacterProfile? rio,
        Dictionary<(string Boss, RaidDifficulty Difficulty), DateTimeOffset> kills)
    {
        if (season is null)
        {
            return [];
        }

        return season.Raids.Select(raid =>
        {
            var progress = rio?.RaidProgression.GetValueOrDefault(raid.Slug);
            var thisWeek = Enum.GetValues<RaidDifficulty>().ToDictionary(
                d => d,
                d => raid.Encounters
                    .Select(e => kills.TryGetValue((NormalizeName(e.Name), d), out var at) && at >= season.CurrentResetUtc)
                    .ToArray());

            return new RaidLockout(
                raid.Slug,
                string.IsNullOrWhiteSpace(progress?.Summary) ? $"0/{raid.Encounters.Count}" : progress.Summary,
                progress?.TotalBosses ?? raid.Encounters.Count,
                progress?.NormalBossesKilled ?? 0,
                progress?.HeroicBossesKilled ?? 0,
                progress?.MythicBossesKilled ?? 0,
                thisWeek);
        }).ToList();
    }

    private static List<EquippedItem> BuildEquipment(BzEquipment? equipment, RioGear? gear)
    {
        var rioItems = gear?.Items ?? [];
        var rioById = rioItems.Values.GroupBy(i => i.ItemId).ToDictionary(g => g.Key, g => g.First());

        IEnumerable<EquippedItem> items;
        if (equipment is { EquippedItems.Count: > 0 })
        {
            items = equipment.EquippedItems.Select(i =>
            {
                var slot = i.Slot.Type ?? "";
                var rioItem = rioById.GetValueOrDefault(i.Item.Id);
                return new EquippedItem(
                    slot,
                    i.Slot.Name ?? SlotNames.GetValueOrDefault(slot, slot),
                    i.Item.Id,
                    i.Name,
                    i.Level?.Value ?? rioItem?.ItemLevel ?? 0,
                    i.Quality?.Type ?? "COMMON",
                    IconUrl(rioItem?.Icon),
                    i.Enchantments.Select(e => CleanEnchantment(e.DisplayString)).OfType<string>().ToList(),
                    i.Sockets.Count,
                    BonusString(i.BonusList.Count > 0 ? i.BonusList : rioItem?.Bonuses));
            });
        }
        else
        {
            items = rioItems.Select(kv =>
            {
                var slot = RioSlotToBlizzard.GetValueOrDefault(kv.Key, kv.Key.ToUpperInvariant());
                var i = kv.Value;
                return new EquippedItem(
                    slot,
                    SlotNames.GetValueOrDefault(slot, slot),
                    i.ItemId,
                    i.Name,
                    i.ItemLevel,
                    Qualities.ElementAtOrDefault(i.ItemQuality) ?? "COMMON",
                    IconUrl(i.Icon),
                    i.EnchantsDetail.Select(e => e.Name).OfType<string>().ToList(),
                    i.Gems.Count,
                    BonusString(i.Bonuses));
            });
        }

        return items
            .OrderBy(i => Array.IndexOf(SlotOrder, i.Slot) is var idx and >= 0 ? idx : int.MaxValue)
            .ToList();
    }

    /// <summary>
    /// « Enchanté : Enchantement de casque - rune… |A:Professions-ChatIcon-Quality-12-Tier2:20:20|a »
    /// → « Enchantement de casque - rune… » (retire le balisage du client WoW, le préfixe et les tirets longs).
    /// </summary>
    private static string? CleanEnchantment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var text = WowMarkup().Replace(value, "");
        text = EnchantPrefix().Replace(text, "");
        text = text.Replace('\u2013', '-').Replace('\u2014', '-');
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"\|A:[^|]*\|a|\|T[^|]*\|t|\|c[0-9a-fA-F]{8}|\|r")]
    private static partial System.Text.RegularExpressions.Regex WowMarkup();

    [System.Text.RegularExpressions.GeneratedRegex(@"^\s*(Enchanté|Enchanted)\s*:\s*")]
    private static partial System.Text.RegularExpressions.Regex EnchantPrefix();

    private static string? IconUrl(string? icon) =>
        string.IsNullOrWhiteSpace(icon) ? null : $"https://wow.zamimg.com/images/wow/icons/medium/{icon}.jpg";

    private static string? BonusString(List<int>? bonuses) =>
        bonuses is { Count: > 0 } ? string.Join(':', bonuses) : null;

    private static RaidDifficulty? ParseDifficulty(string? type) => type?.ToUpperInvariant() switch
    {
        "LFR" => RaidDifficulty.Lfr,
        "NORMAL" => RaidDifficulty.Normal,
        "HEROIC" => RaidDifficulty.Heroic,
        "MYTHIC" => RaidDifficulty.Mythic,
        _ => null
    };

    private static string DifficultyLabel(RaidDifficulty difficulty) => difficulty switch
    {
        RaidDifficulty.Lfr => "RdR",
        RaidDifficulty.Normal => "N",
        RaidDifficulty.Heroic => "H",
        _ => "M"
    };

    private static bool IsActive(RioRegionDates starts, RioRegionDates ends, string region, DateTimeOffset now) =>
        starts.For(region) is { } start && start <= now && (ends.For(region) is not { } end || now < end);

    /// <summary>La réinitialisation hebdo tombe toujours à la même heure que le début de saison de la région.</summary>
    private static (DateTimeOffset Current, DateTimeOffset Next) ComputeWeeklyReset(DateTimeOffset? seasonStart, string region, DateTimeOffset now)
    {
        var anchor = seasonStart ?? region switch
        {
            "us" => new DateTimeOffset(2026, 8, 18, 15, 0, 0, TimeSpan.Zero),
            "eu" => new DateTimeOffset(2026, 8, 19, 4, 0, 0, TimeSpan.Zero),
            _ => new DateTimeOffset(2026, 8, 19, 23, 0, 0, TimeSpan.Zero)
        };

        var weeks = Math.Floor((now - anchor).TotalDays / 7);
        var current = anchor.AddDays(weeks * 7);
        return (current, current.AddDays(7));
    }

    private static string NormalizeRegion(string region)
    {
        region = region.Trim().ToLowerInvariant();
        return Regions.All.Contains(region) ? region : "eu";
    }

    /// <summary>« Confrérie du Thorium » → « confrerie-du-thorium », « Kael'thas » → « kaelthas ».</summary>
    public static string Slugify(string value)
    {
        var builder = new StringBuilder();
        foreach (var c in value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark || c is '\'' or '’')
            {
                continue;
            }

            builder.Append(char.IsWhiteSpace(c) ? '-' : c);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string NormalizeName(string? value) =>
        new((value ?? "").Normalize(NormalizationForm.FormD)
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());

    private async Task<T?> SafeAsync<T>(Func<Task<T?>> call, string source, List<string>? warnings = null) where T : class
    {
        try
        {
            return await call();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            logger.LogWarning(ex, "Échec de l'appel {Source}", source);
            if (warnings is not null)
            {
                lock (warnings)
                {
                    warnings.Add($"Source {source} indisponible pour le moment.");
                }
            }

            return null;
        }
    }
}
