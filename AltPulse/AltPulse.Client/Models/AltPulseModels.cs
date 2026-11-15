namespace AltPulse.Client.Models;

public static class Regions
{
    public static readonly string[] All = ["eu", "us", "kr", "tw"];
}

public enum RaidDifficulty
{
    Lfr = 0,
    Normal = 1,
    Heroic = 2,
    Mythic = 3
}

public sealed record SeasonInfo(
    string Region,
    string Slug,
    string Name,
    DateTimeOffset CurrentResetUtc,
    DateTimeOffset NextResetUtc,
    IReadOnlyList<DungeonInfo> Dungeons,
    IReadOnlyList<RaidInfo> Raids,
    AffixWeek? Affixes);

public sealed record DungeonInfo(
    string Slug,
    string Name,
    string ShortName,
    int ChallengeModeId,
    int TimerSeconds,
    string? IconUrl,
    string? BackgroundUrl);

public sealed record RaidInfo(
    string Slug,
    string Name,
    string ShortName,
    IReadOnlyList<EncounterInfo> Encounters);

public sealed record EncounterInfo(string Slug, string Name);

public sealed record AffixWeek(string Title, IReadOnlyList<AffixInfo> Affixes);

public sealed record AffixInfo(int Id, string Name, string Description, string? IconUrl, string? WowheadUrl);

public sealed record RealmInfo(string Slug, string Name);

public sealed record CharacterSnapshot
{
    public required string Region { get; init; }
    public required string RealmSlug { get; init; }
    public required string RealmName { get; init; }
    public required string Name { get; init; }
    public int Level { get; init; }
    public int ClassId { get; init; }
    public string? ClassName { get; init; }
    public string? SpecName { get; init; }
    public string? Role { get; init; }
    public string? RaceName { get; init; }
    public string? Faction { get; init; }
    public string? GuildName { get; init; }
    public string? AvatarUrl { get; init; }
    public string? InsetUrl { get; init; }
    public string? RaiderIoUrl { get; init; }
    public string? ArmoryUrl { get; init; }

    public double? ItemLevelEquipped { get; init; }
    public double? ItemLevelAverage { get; init; }

    public double Rating { get; init; }
    public string? RatingColor { get; init; }

    public IReadOnlyList<DungeonBest> DungeonBests { get; init; } = [];
    public IReadOnlyList<WeeklyRun> WeeklyRuns { get; init; } = [];
    public IReadOnlyList<WeeklyRun> PreviousWeeklyRuns { get; init; } = [];

    public VaultProgress Vault { get; init; } = new([], [], []);
    public IReadOnlyList<RaidLockout> Raids { get; init; } = [];
    public IReadOnlyList<EquippedItem> Equipment { get; init; } = [];

    /// <summary>Clé en poche : connue seulement par l'addon NoovioPulse.</summary>
    public OwnedKeystone? Keystone { get; init; }

    /// <summary>Dernière relecture en jeu par l'addon, quand ses données complètent celles des API.</summary>
    public DateTimeOffset? AddonSyncedAtUtc { get; init; }

    public DateTimeOffset FetchedAtUtc { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];

    public string Key => CharacterKey.Of(Region, RealmSlug, Name);
}

public sealed record DungeonBest(
    string DungeonSlug,
    string ShortName,
    int Level,
    int Upgrades,
    double Score,
    long ClearTimeMs,
    long ParTimeMs,
    string? Url);

public sealed record WeeklyRun(string ShortName, string Dungeon, int Level, int Upgrades, DateTimeOffset CompletedAt);

/// <summary>Une case de la Grande Chambre forte : null = pas encore débloquée.</summary>
public sealed record VaultSlot(int Threshold, int Progress, string? Reward);

public sealed record VaultProgress(
    IReadOnlyList<VaultSlot> Dungeons,
    IReadOnlyList<VaultSlot> Raids,
    IReadOnlyList<VaultSlot> PreviousWeekDungeons);

public sealed record RaidLockout(
    string RaidSlug,
    string Summary,
    int TotalBosses,
    int NormalKilled,
    int HeroicKilled,
    int MythicKilled,
    /// <summary>Clé = difficulté, valeur = un booléen par boss (ordre du raid) : tué depuis la dernière réinitialisation.</summary>
    IReadOnlyDictionary<RaidDifficulty, bool[]> KilledThisWeek);

public sealed record EquippedItem(
    string Slot,
    string SlotName,
    int ItemId,
    string Name,
    int ItemLevel,
    string Quality,
    string? IconUrl,
    IReadOnlyList<string> Enchantments,
    int Sockets,
    string? Bonuses);

public sealed record CharacterLookupResult(CharacterSnapshot? Character, string? Error);

public static class CharacterKey
{
    public static string Of(string region, string realmSlug, string name) =>
        $"{region}/{realmSlug}/{name}".ToLowerInvariant();
}

