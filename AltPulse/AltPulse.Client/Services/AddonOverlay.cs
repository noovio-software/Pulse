using AltPulse.Client.Models;

namespace AltPulse.Client.Services;

/// <summary>
/// Complète la fiche d'un personnage (raider.io + Blizzard) avec ce que l'addon NoovioPulse a lu en jeu :
/// Grande Chambre forte exacte (raid compris), clés de la semaine, clé en poche. Les données de l'addon
/// ne valent que pour la semaine où elles ont été relues : d'une semaine passée, seul le coffre M+ sert
/// (ligne « semaine passée ») ; plus anciennes, elles sont ignorées.
/// </summary>
public static class AddonOverlay
{
    private const int DungeonActivity = 1;
    private const int RaidActivity = 3;
    private static readonly TimeSpan Week = TimeSpan.FromDays(7);

    /// <summary>Tolérance sur l'heure de réinitialisation : le jeu et raider.io peuvent différer de quelques minutes.</summary>
    private static readonly TimeSpan Slack = TimeSpan.FromHours(1);

    public static CharacterSnapshot Apply(CharacterSnapshot? snapshot, AddonCharacter addon, SeasonInfo? season, DateTimeOffset now)
    {
        var baseline = snapshot ?? FromAddon(addon);
        var currentReset = season is not null && season.Region == addon.Region
            ? season.CurrentResetUtc
            : addon.WeekStartUtc is { } start && start + Week > now ? start : now - Week;
        var week = addon.WeekStartUtc is { } weekStart
            ? weekStart >= currentReset - Slack ? 0 : weekStart >= currentReset - Week - Slack ? 1 : 2
            : 2;

        var vault = baseline.Vault;
        var runs = baseline.WeeklyRuns;
        var keystone = baseline.Keystone;
        if (week == 0)
        {
            var dungeons = Slots(addon, DungeonActivity, level => level > 0 ? $"+{level}" : "M0");
            var raids = Slots(addon, RaidActivity, DifficultyLabel);
            vault = vault with
            {
                Dungeons = dungeons.Count > 0 ? dungeons : vault.Dungeons,
                Raids = raids.Count > 0 ? raids : vault.Raids
            };
            runs = addon.Runs.Count > 0 || runs.Count == 0 ? addon.Runs.Select(r => ToRun(r, season, addon.SavedAtUtc)).ToList() : runs;
            keystone = addon.Keystone is { } key ? ToKeystone(key, season) : null;
        }
        else if (week == 1)
        {
            var previous = Slots(addon, DungeonActivity, level => level > 0 ? $"+{level}" : "M0");
            vault = vault with { PreviousWeekDungeons = previous.Count > 0 ? previous : vault.PreviousWeekDungeons };
        }

        return baseline with
        {
            Level = Math.Max(baseline.Level, addon.Level),
            SpecName = baseline.SpecName ?? addon.SpecName,
            ItemLevelEquipped = Newer(baseline, addon) && addon.ItemLevelEquipped > 0 ? addon.ItemLevelEquipped : baseline.ItemLevelEquipped ?? addon.ItemLevelEquipped,
            ItemLevelAverage = Newer(baseline, addon) && addon.ItemLevelAverage > 0 ? addon.ItemLevelAverage : baseline.ItemLevelAverage ?? addon.ItemLevelAverage,
            Rating = week == 0 ? Math.Max(baseline.Rating, addon.Rating) : baseline.Rating,
            RatingColor = week == 0 && addon.Rating > baseline.Rating ? addon.RatingColor ?? baseline.RatingColor : baseline.RatingColor,
            Vault = vault,
            WeeklyRuns = runs,
            Keystone = keystone,
            AddonSyncedAtUtc = addon.SavedAtUtc
        };
    }

    /// <summary>Fiche minimale quand le personnage est introuvable en ligne (trop bas niveau, profil privé, API en panne).</summary>
    private static CharacterSnapshot FromAddon(AddonCharacter addon) => new()
    {
        Region = addon.Region,
        RealmSlug = addon.RealmSlug,
        RealmName = addon.RealmName,
        Name = addon.Name,
        Level = addon.Level,
        ClassId = addon.ClassId,
        SpecName = addon.SpecName,
        Role = addon.Role?.ToUpperInvariant() switch
        {
            "TANK" => "tank",
            "HEALER" => "healer",
            "DAMAGER" => "dps",
            _ => null
        },
        RaceName = addon.RaceName,
        Faction = addon.Faction?.ToLowerInvariant(),
        RaiderIoUrl = $"https://raider.io/characters/{addon.Region}/{addon.RealmSlug}/{Uri.EscapeDataString(addon.Name)}",
        ArmoryUrl = $"https://worldofwarcraft.blizzard.com/fr-fr/character/{addon.Region}/{addon.RealmSlug}/{Uri.EscapeDataString(addon.Name.ToLowerInvariant())}",
        ItemLevelEquipped = addon.ItemLevelEquipped,
        ItemLevelAverage = addon.ItemLevelAverage,
        Rating = addon.Rating,
        RatingColor = addon.RatingColor,
        FetchedAtUtc = addon.SavedAtUtc,
        Warnings = ["Introuvable sur raider.io et l'armurerie : seules les données de l'addon sont affichées."]
    };

    private static bool Newer(CharacterSnapshot snapshot, AddonCharacter addon) => addon.SavedAtUtc > snapshot.FetchedAtUtc;

    private static List<VaultSlot> Slots(AddonCharacter addon, int type, Func<int, string> reward) =>
        addon.Vault
            .Where(a => a.Type == type && a.Threshold > 0)
            .OrderBy(a => a.Index)
            .Select(a => new VaultSlot(a.Threshold, Math.Min(a.Progress, a.Threshold), a.Progress >= a.Threshold ? reward(a.Level) : null))
            .ToList();

    /// <summary>Niveau d'une case de raid = identifiant de difficulté du jeu.</summary>
    private static string DifficultyLabel(int difficultyId) => difficultyId switch
    {
        17 or 7 => "RdR",
        14 or 3 or 4 => "N",
        15 or 5 or 6 => "H",
        16 => "M",
        _ => "✓"
    };

    private static WeeklyRun ToRun(AddonRun run, SeasonInfo? season, DateTimeOffset at)
    {
        var dungeon = season?.Dungeons.FirstOrDefault(d => d.ChallengeModeId == run.MapId);
        var name = dungeon?.Name ?? run.MapName ?? $"Donjon {run.MapId}";
        return new WeeklyRun(dungeon?.ShortName ?? Short(name), name, run.Level, run.Completed ? 1 : 0, at);
    }

    private static OwnedKeystone ToKeystone(AddonKeystone key, SeasonInfo? season)
    {
        var dungeon = season?.Dungeons.FirstOrDefault(d => d.ChallengeModeId == key.MapId);
        var name = dungeon?.Name ?? key.MapName ?? $"Donjon {key.MapId}";
        return new OwnedKeystone(dungeon?.ShortName ?? Short(name), name, key.Level);
    }

    /// <summary>« The Dawnbreaker » → « TD » : faute de nom court raider.io (donjon hors saison, autre région).</summary>
    private static string Short(string name)
    {
        var words = name.Split([' ', '-', '\''], StringSplitOptions.RemoveEmptyEntries);
        return words.Length > 1
            ? string.Concat(words.Select(w => char.ToUpperInvariant(w[0])))
            : name[..Math.Min(4, name.Length)].ToUpperInvariant();
    }
}
