using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Noovio.Pulse.Application.Abstractions;
using Noovio.Pulse.Application.Ingestion;
using Noovio.Pulse.Domain.Roles;
using Noovio.Pulse.Domain.Enums;

namespace Noovio.Pulse.Infrastructure.RaiderIo;

/// <summary>
/// Parcourt le classement des clés de la saison pour chaque (région, donjon), garde chaque joueur du rôle
/// sur sa meilleure clé toutes requêtes confondues, puis classe chaque spécialisation par ce score.
/// </summary>
public sealed class RaiderIoLeaderboardSource(
    RaiderIoClient client,
    PulseRole role,
    IDistributedCache cache,
    IOptions<RaiderIoOptions> options,
    TimeProvider time,
    ILogger<RaiderIoLeaderboardSource> logger) : ILeaderboardSource, ISeasonDungeonSource
{
    private readonly RaiderIoOptions _options = options.Value;

    public async Task<string> GetCurrentSeasonAsync(CancellationToken ct) => (await GetCurrentSeasonInfoAsync(ct)).Slug;

    public async Task<SeasonDungeonList> GetCurrentDungeonsAsync(CancellationToken ct)
    {
        var season = await GetCurrentSeasonInfoAsync(ct);
        return new SeasonDungeonList(
            season.Slug,
            season.Dungeons
                .Select(d => new SeasonDungeonInfo(d.Slug, d.Name, d.ShortName, d.ChallengeModeId, d.KeystoneTimerSeconds, d.IconUrl, d.BackgroundImageUrl))
                .ToList());
    }

    public async Task<IReadOnlyDictionary<WowSpec, IReadOnlyList<LeaderboardPlayer>>> GetTopPlayersAsync(string season, int perSpec, CancellationToken ct,
        IProgress<LeaderboardStep>? progress = null)
    {
        var key = $"rio:top-players:{season}:{perSpec}";
        if (await cache.GetStringAsync(key, ct) is { } cached)
        {
            return Group(JsonSerializer.Deserialize<List<LeaderboardPlayer>>(cached, RaiderIoClient.JsonOptions) ?? [])
                .ToDictionary(kv => kv.Key, kv => (IReadOnlyList<LeaderboardPlayer>)kv.Value);
        }

        var dungeons = (await GetCurrentSeasonInfoAsync(ct)).Dungeons.Select(d => d.Slug).ToList();
        var best = new Dictionary<long, LeaderboardPlayer>();
        var requests = 0;
        var queries = _options.Regions.Length * dungeons.Count;
        var queriesDone = 0;
        progress?.Report(new LeaderboardStep(0, queries));

        foreach (var region in _options.Regions)
        {
            foreach (var dungeon in dungeons)
            {
                for (var page = 0; page < _options.MaxPagesPerQuery; page++)
                {
                    var runs = await GetPageAsync(season, region, dungeon, page, ct);
                    requests++;
                    if (runs is null || runs.Rankings.Count == 0)
                    {
                        break;
                    }

                    foreach (var ranking in runs.Rankings)
                    {
                        Collect(ranking, best);
                    }
                }

                progress?.Report(new LeaderboardStep(++queriesDone, queries));
            }
        }

        var top = Group(best.Values).ToDictionary(kv => kv.Key, kv => (IReadOnlyList<LeaderboardPlayer>)kv.Value.Take(perSpec).ToList());
        logger.LogInformation("Classement {Season} : {Requests} pages lues, {Players} joueurs distincts, {Counts}", season, requests, best.Count,
            string.Join(", ", top.Select(kv => $"{kv.Key}={kv.Value.Count}")));

        await cache.SetStringAsync(
            key,
            JsonSerializer.Serialize(top.Values.SelectMany(l => l), RaiderIoClient.JsonOptions),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = _options.LeaderboardCacheDuration },
            ct);
        return top;
    }

    /// <summary>
    /// Une page en erreur (timeout, 5xx) est retentée deux fois ; au-delà, on renonce à la suite de cette
    /// requête (région, donjon) plutôt qu'à tout le classement.
    /// </summary>
    private async Task<RioRunsPage?> GetPageAsync(string season, string region, string dungeon, int page, CancellationToken ct)
    {
        const int attempts = 3;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await client.GetRunsAsync(season, region, dungeon, page, ct);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                if (attempt == attempts)
                {
                    logger.LogWarning(ex, "Classement {Region}/{Dungeon} interrompu page {Page}", region, dungeon, page);
                    return null;
                }

                await Task.Delay(TimeSpan.FromSeconds(2 * attempt), ct);
            }
        }
    }

    /// <summary>Retient le joueur du rôle de la clé s'il est nouveau ou si cette clé bat sa meilleure connue.</summary>
    private void Collect(RioRanking ranking, Dictionary<long, LeaderboardPlayer> best)
    {
        foreach (var member in ranking.Run.Roster)
        {
            var c = member.Character;
            if (member.Role != role.RaiderIoRole
                || c.Spec is not { } spec || !role.Includes(spec.Id)
                || c.Region is not { } region
                || c.Realm is not { } realm
                || (best.TryGetValue(c.Id, out var known) && known.BestRunScore >= ranking.Score))
            {
                continue;
            }

            best[c.Id] = new LeaderboardPlayer(
                c.Id,
                (WowSpec)spec.Id,
                c.Name,
                region.Slug,
                realm.Slug,
                c.Path is null ? null : "https://raider.io" + c.Path,
                ranking.Score,
                ranking.Run.MythicLevel,
                string.IsNullOrEmpty(member.Loadout) ? null : member.Loadout);
        }
    }

    private Dictionary<WowSpec, List<LeaderboardPlayer>> Group(IEnumerable<LeaderboardPlayer> players)
    {
        var bySpec = players.ToLookup(p => p.Spec);
        return role.Specs.ToDictionary(
            s => s.Spec,
            s => bySpec[s.Spec].OrderByDescending(t => t.BestRunScore).ThenByDescending(t => t.BestKeyLevel).ToList());
    }

    private async Task<RioSeason> GetCurrentSeasonInfoAsync(CancellationToken ct)
    {
        var data = await client.GetMythicPlusStaticDataAsync(ct)
                   ?? throw new InvalidOperationException("Données statiques M+ Raider.io indisponibles.");

        var now = time.GetUtcNow();
        return data.Seasons
                   .Where(s => s.IsMainSeason && s.Starts.Us <= now)
                   .OrderByDescending(s => s.Starts.Us)
                   .FirstOrDefault()
               ?? data.Seasons.FirstOrDefault(s => s.IsMainSeason)
               ?? throw new InvalidOperationException("Aucune saison M+ en cours chez Raider.io.");
    }
}
