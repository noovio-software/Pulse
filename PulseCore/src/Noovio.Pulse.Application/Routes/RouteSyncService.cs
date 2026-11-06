using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Noovio.Pulse.Application.Abstractions;
using Noovio.Pulse.Domain.Entities;

namespace Noovio.Pulse.Application.Routes;

public sealed class RouteSyncOptions
{
    public const string SectionName = "Routes";

    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(6);

    /// <summary>Routes lues par donjon, avec une marge : certaines sont écartées (expirées, incomplètes, saison passée).</summary>
    public int CandidatesPerDungeon { get; set; } = 15;

    /// <summary>Routes gardées par donjon.</summary>
    public int RoutesPerDungeon { get; set; } = 8;
}

public sealed record RouteSyncReport(string Season, int Updated, int Failed);

/// <summary>
/// Synchronise les routes de chaque donjon de la saison. Un donjon en échec garde ses routes précédentes,
/// et n'empêche pas de mettre à jour les autres.
/// </summary>
public sealed class RouteSyncService(
    ISeasonDungeonSource seasons,
    IDungeonRouteSource routes,
    IDungeonRouteStore store,
    IEncounterJournalSource journal,
    IOptions<RouteSyncOptions> options,
    TimeProvider time,
    ILogger<RouteSyncService> logger)
{
    private readonly RouteSyncOptions _options = options.Value;

    public async Task<RouteSyncReport> RunAsync(CancellationToken ct)
    {
        var season = await seasons.GetCurrentDungeonsAsync(ct);
        var slugs = await routes.GetDungeonSlugsAsync(ct);
        var localNames = await GetLocalNamesAsync(season.Season, ct);

        var updated = 0;
        var failed = 0;
        foreach (var dungeon in season.Dungeons)
        {
            var slug = slugs.GetValueOrDefault(dungeon.ChallengeModeId, dungeon.Slug);
            try
            {
                var kept = await ReadRoutesAsync(slug, ct);
                if (kept.Count == 0)
                {
                    logger.LogWarning("Aucune route exploitable pour {Dungeon}, routes précédentes conservées", slug);
                    failed++;
                    continue;
                }

                await store.ReplaceAsync(new SeasonDungeon
                {
                    Season = season.Season,
                    Slug = slug,
                    Name = localNames.GetValueOrDefault(dungeon.Name, dungeon.Name),
                    ShortName = dungeon.ShortName,
                    TimerSeconds = dungeon.TimerSeconds,
                    IconUrl = dungeon.IconUrl,
                    ImageUrl = dungeon.ImageUrl,
                    Routes = [.. kept],
                    FetchedAtUtc = time.GetUtcNow()
                }, ct);
                updated++;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Routes de {Dungeon} non synchronisées, routes précédentes conservées", slug);
                failed++;
            }
        }

        return new RouteSyncReport(season.Season, updated, failed);
    }

    private async Task<IReadOnlyList<DungeonRoute>> ReadRoutesAsync(string slug, CancellationToken ct)
    {
        var read = new List<DungeonRoute>();
        foreach (var key in await routes.GetPopularRouteKeysAsync(slug, _options.CandidatesPerDungeon, ct))
        {
            if (await routes.GetRouteAsync(key, ct) is { } route)
            {
                read.Add(route);
            }
        }

        return RouteSelection.Keep(read, time.GetUtcNow(), _options.RoutesPerDungeon);
    }

    /// <summary>Noms du Journal d'aventure, dans la langue du site ; à défaut, le nom anglais reste affiché.</summary>
    private async Task<IReadOnlyDictionary<string, string>> GetLocalNamesAsync(string season, CancellationToken ct)
    {
        var instances = (await journal.GetAsync(season, ct))?.Instances ?? [];
        return instances
            .Where(i => i.Kind == InstanceKind.Dungeon)
            .GroupBy(i => i.RaiderIoName)
            .ToDictionary(g => g.Key, g => g.First().Name);
    }
}
