using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AltPulse.Services.RaiderIo;

/// <summary>Client de l'API publique Raider.io (https://raider.io/api).</summary>
public sealed class RaiderIoClient(HttpClient http)
{
    /// <summary>Midnight.</summary>
    public const int ExpansionId = 11;

    private const string ProfileFields =
        "mythic_plus_scores_by_season:current," +
        "mythic_plus_best_runs:all," +
        "mythic_plus_weekly_highest_level_runs," +
        "mythic_plus_previous_weekly_highest_level_runs," +
        "raid_progression," +
        "gear," +
        "guild";

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public Task<RioMythicPlusStaticData?> GetMythicPlusStaticDataAsync(CancellationToken ct) =>
        GetAsync<RioMythicPlusStaticData>($"api/v1/mythic-plus/static-data?expansion_id={ExpansionId}", ct);

    public Task<RioRaidStaticData?> GetRaidStaticDataAsync(CancellationToken ct) =>
        GetAsync<RioRaidStaticData>($"api/v1/raiding/static-data?expansion_id={ExpansionId}", ct);

    public Task<RioAffixes?> GetAffixesAsync(string region, CancellationToken ct) =>
        GetAsync<RioAffixes>($"api/v1/mythic-plus/affixes?region={region}&locale=fr", ct);

    public Task<RioCharacterProfile?> GetCharacterAsync(string region, string realm, string name, CancellationToken ct) =>
        GetAsync<RioCharacterProfile>(
            $"api/v1/characters/profile?region={region}&realm={Uri.EscapeDataString(realm)}&name={Uri.EscapeDataString(name)}&fields={ProfileFields}",
            ct);

    private async Task<T?> GetAsync<T>(string url, CancellationToken ct) where T : class
    {
        using var response = await http.GetAsync(url, ct);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest)
        {
            // Raider.io répond 400 « Could not find requested character » pour un personnage inconnu.
            return null;
        }

        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, ct);
    }
}

public sealed class RioRegionDates
{
    public DateTimeOffset? Us { get; set; }
    public DateTimeOffset? Eu { get; set; }
    public DateTimeOffset? Tw { get; set; }
    public DateTimeOffset? Kr { get; set; }

    public DateTimeOffset? For(string region) => region switch
    {
        "us" => Us,
        "eu" => Eu,
        "tw" => Tw,
        "kr" => Kr,
        _ => Eu
    };
}

public sealed class RioMythicPlusStaticData
{
    public List<RioSeason> Seasons { get; set; } = [];
}

public sealed class RioSeason
{
    public string Slug { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsMainSeason { get; set; }
    public RioRegionDates Starts { get; set; } = new();
    public RioRegionDates Ends { get; set; } = new();
    public List<RioDungeon> Dungeons { get; set; } = [];
}

public sealed class RioDungeon
{
    public int Id { get; set; }
    public int ChallengeModeId { get; set; }
    public string Slug { get; set; } = "";
    public string Name { get; set; } = "";
    public string ShortName { get; set; } = "";
    public int KeystoneTimerSeconds { get; set; }
    public string? IconUrl { get; set; }
    public string? BackgroundImageUrl { get; set; }
}

public sealed class RioRaidStaticData
{
    public List<RioRaid> Raids { get; set; } = [];
}

public sealed class RioRaid
{
    public int Id { get; set; }
    public string Slug { get; set; } = "";
    public string Name { get; set; } = "";
    public string ShortName { get; set; } = "";
    public RioRegionDates Starts { get; set; } = new();
    public RioRegionDates Ends { get; set; } = new();
    public List<RioEncounter> Encounters { get; set; } = [];
}

public sealed class RioEncounter
{
    public int Id { get; set; }
    public string Slug { get; set; } = "";
    public string Name { get; set; } = "";
}

public sealed class RioAffixes
{
    public string Title { get; set; } = "";
    public List<RioAffix> AffixDetails { get; set; } = [];
}

public sealed class RioAffix
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string? IconUrl { get; set; }
    public string? WowheadUrl { get; set; }
}

public sealed class RioCharacterProfile
{
    public string Name { get; set; } = "";
    public string? Race { get; set; }
    public string? Class { get; set; }
    public string? ActiveSpecName { get; set; }
    public string? ActiveSpecRole { get; set; }
    public string? Faction { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string Region { get; set; } = "";
    public string Realm { get; set; } = "";
    public string? ProfileUrl { get; set; }
    public RioGuild? Guild { get; set; }
    public List<RioSeasonScore> MythicPlusScoresBySeason { get; set; } = [];
    public List<RioRun> MythicPlusBestRuns { get; set; } = [];
    public List<RioRun> MythicPlusWeeklyHighestLevelRuns { get; set; } = [];
    public List<RioRun> MythicPlusPreviousWeeklyHighestLevelRuns { get; set; } = [];
    public Dictionary<string, RioRaidProgress> RaidProgression { get; set; } = [];
    public RioGear? Gear { get; set; }
}

public sealed class RioGuild
{
    public string Name { get; set; } = "";
}

public sealed class RioSeasonScore
{
    public string Season { get; set; } = "";
    public Dictionary<string, RioScoreSegment> Segments { get; set; } = [];
}

public sealed class RioScoreSegment
{
    public double Score { get; set; }
    public string? Color { get; set; }
}

public sealed class RioRun
{
    public string Dungeon { get; set; } = "";
    public string ShortName { get; set; } = "";
    public int MythicLevel { get; set; }
    public DateTimeOffset CompletedAt { get; set; }
    public long ClearTimeMs { get; set; }
    public long ParTimeMs { get; set; }
    public int NumKeystoneUpgrades { get; set; }
    public int MapChallengeModeId { get; set; }
    public double Score { get; set; }
    public string? Url { get; set; }
}

public sealed class RioRaidProgress
{
    public string Summary { get; set; } = "";
    public int TotalBosses { get; set; }
    public int NormalBossesKilled { get; set; }
    public int HeroicBossesKilled { get; set; }
    public int MythicBossesKilled { get; set; }
}

public sealed class RioGear
{
    public double ItemLevelEquipped { get; set; }
    public Dictionary<string, RioGearItem> Items { get; set; } = [];
}

public sealed class RioGearItem
{
    public int ItemId { get; set; }
    public int ItemLevel { get; set; }
    public string? Icon { get; set; }
    public string Name { get; set; } = "";
    public int ItemQuality { get; set; }
    public List<int> Bonuses { get; set; } = [];
    public List<int> Gems { get; set; } = [];
    public List<RioEnchantDetail> EnchantsDetail { get; set; } = [];
}

public sealed class RioEnchantDetail
{
    public int Id { get; set; }
    public string? Name { get; set; }
}
