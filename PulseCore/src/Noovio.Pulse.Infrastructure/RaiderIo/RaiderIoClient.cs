using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace Noovio.Pulse.Infrastructure.RaiderIo;

public sealed class RaiderIoOptions
{
    public const string SectionName = "RaiderIo";

    /// <summary>Midnight.</summary>
    public int ExpansionId { get; set; } = 11;

    /// <summary>
    /// Le classement est lu région par région et donjon par donjon : l'API plafonne chaque requête
    /// à 101 pages de 20 clés (page 101 → 400), et le haut du classement mondial « all » ne compte
    /// que quelques dizaines de joueurs distincts par rôle. Pas de « cn » : l'API Blizzard globale ne le sert pas.
    /// </summary>
    public string[] Regions { get; set; } = ["us", "eu", "kr", "tw"];

    /// <summary>Plafond de l'API : pages 0 à 100.</summary>
    public int MaxPagesPerQuery { get; set; } = 101;

    public int RequestsPerSecond { get; set; } = 5;

    /// <summary>Durée de cache de la liste de joueurs extraite (les pages brutes ne sont pas gardées : plusieurs centaines de Mo).</summary>
    public TimeSpan LeaderboardCacheDuration { get; set; } = TimeSpan.FromHours(1);
}

/// <summary>
/// Client de l'API publique Raider.io (https://raider.io/api), sans clé.
/// Seules les données statiques sont mises en cache ici ; le classement l'est une fois réduit aux joueurs du rôle
/// (voir <see cref="RaiderIoLeaderboardSource"/>).
/// </summary>
public sealed class RaiderIoClient(HttpClient http, IDistributedCache cache, IOptions<RaiderIoOptions> options)
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    private readonly RaiderIoOptions _options = options.Value;

    public Task<RioMythicPlusStaticData?> GetMythicPlusStaticDataAsync(CancellationToken ct) =>
        GetAsync<RioMythicPlusStaticData>($"api/v1/mythic-plus/static-data?expansion_id={_options.ExpansionId}", TimeSpan.FromHours(12), ct);

    public Task<RioRaidingStaticData?> GetRaidingStaticDataAsync(CancellationToken ct) =>
        GetAsync<RioRaidingStaticData>($"api/v1/raiding/static-data?expansion_id={_options.ExpansionId}", TimeSpan.FromHours(12), ct);

    /// <summary>Profil M+ d'un personnage (null si inconnu de Raider.io, qui répond alors 400).</summary>
    public Task<RioCharacterProfile?> GetCharacterAsync(string region, string realmSlug, string name, CancellationToken ct) =>
        GetAsync<RioCharacterProfile>(
            $"api/v1/characters/profile?region={region}&realm={Uri.EscapeDataString(realmSlug)}&name={Uri.EscapeDataString(name)}" +
            "&fields=mythic_plus_scores_by_season:current,mythic_plus_best_runs:all,mythic_plus_ranks",
            null,
            ct);

    /// <summary>null au-delà de la dernière page (l'API répond 400).</summary>
    public Task<RioRunsPage?> GetRunsAsync(string season, string region, string dungeon, int page, CancellationToken ct) =>
        GetAsync<RioRunsPage>(
            $"api/v1/mythic-plus/runs?season={Uri.EscapeDataString(season)}&region={region}&dungeon={Uri.EscapeDataString(dungeon)}&page={page}",
            null,
            ct);

    private async Task<T?> GetAsync<T>(string url, TimeSpan? cacheDuration, CancellationToken ct) where T : class
    {
        var key = "rio:" + url;
        var json = cacheDuration is null ? null : await cache.GetStringAsync(key, ct);
        if (json is null)
        {
            using var response = await http.GetAsync(url, ct);
            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();
            json = await response.Content.ReadAsStringAsync(ct);
            if (cacheDuration is { } duration)
            {
                await cache.SetStringAsync(key, json, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = duration }, ct);
            }
        }

        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }
}

public sealed class RioMythicPlusStaticData
{
    public List<RioSeason> Seasons { get; set; } = [];
}

public sealed class RioRaidingStaticData
{
    public List<RioDungeon> Raids { get; set; } = [];
}

public sealed class RioSeason
{
    public string Slug { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsMainSeason { get; set; }
    public RioRegionDates Starts { get; set; } = new();
    public List<RioDungeon> Dungeons { get; set; } = [];
}

public sealed class RioDungeon
{
    public string Slug { get; set; } = "";
    public string Name { get; set; } = "";
    public string ShortName { get; set; } = "";
    public int ChallengeModeId { get; set; }
    public int KeystoneTimerSeconds { get; set; }
    public string? IconUrl { get; set; }
    public string? BackgroundImageUrl { get; set; }
}

public sealed class RioRegionDates
{
    public DateTimeOffset? Us { get; set; }
    public DateTimeOffset? Eu { get; set; }
}

public sealed class RioCharacterProfile
{
    public string? ProfileUrl { get; set; }
    public string? ThumbnailUrl { get; set; }
    public List<RioSeasonScore> MythicPlusScoresBySeason { get; set; } = [];
    public List<RioBestRun> MythicPlusBestRuns { get; set; } = [];
    public RioRanks? MythicPlusRanks { get; set; }
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

public sealed class RioBestRun
{
    public string Dungeon { get; set; } = "";
    public string ShortName { get; set; } = "";
    public int MythicLevel { get; set; }
    public int NumKeystoneUpgrades { get; set; }
    public double Score { get; set; }
    public string? Url { get; set; }
}

public sealed class RioRanks
{
    public RioRank? Tank { get; set; }
    public RioRank? ClassTank { get; set; }
    public RioRank? Healer { get; set; }
    public RioRank? ClassHealer { get; set; }
}

public sealed class RioRank
{
    public int World { get; set; }
    public int Region { get; set; }
    public int Realm { get; set; }
}

public sealed class RioRunsPage
{
    public List<RioRanking> Rankings { get; set; } = [];
}

public sealed class RioRanking
{
    public int Rank { get; set; }
    public double Score { get; set; }
    public RioRun Run { get; set; } = new();
}

public sealed class RioRun
{
    public string Season { get; set; } = "";
    public int MythicLevel { get; set; }
    public List<RioRosterMember> Roster { get; set; } = [];
}

public sealed class RioRosterMember
{
    public RioCharacter Character { get; set; } = new();

    /// <summary>tank, healer, dps.</summary>
    public string Role { get; set; } = "";

    /// <summary>Chaîne d'export des talents utilisée sur cette clé.</summary>
    public string? Loadout { get; set; }
}

public sealed class RioCharacter
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public RioRef? Class { get; set; }
    public RioRef? Spec { get; set; }
    public RioRef? Realm { get; set; }
    public RioRef? Region { get; set; }

    /// <summary>/characters/eu/draenor/Name</summary>
    public string? Path { get; set; }
}

public sealed class RioRef
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
}
