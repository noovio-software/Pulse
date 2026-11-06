using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Noovio.Pulse.Application.Abstractions;
using Noovio.Pulse.Domain.Entities;

namespace Noovio.Pulse.Infrastructure.Persistence;

public sealed class DungeonRouteStore(PulseDbContext db) : IDungeonRouteStore
{
    public async Task ReplaceAsync(SeasonDungeon dungeon, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.SeasonDungeons.Where(d => d.Season == dungeon.Season && d.Slug == dungeon.Slug).ExecuteDeleteAsync(ct);
        db.SeasonDungeons.Add(dungeon);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        db.ChangeTracker.Clear();
    }

    public async Task<DateTimeOffset?> GetLastFetchedAsync(CancellationToken ct) =>
        await db.SeasonDungeons.MaxAsync(d => (DateTimeOffset?)d.FetchedAtUtc, ct);
}

/// <summary>Comme les snapshots : les routes ne changent qu'à chaque synchronisation, quelques minutes de cache suffisent.</summary>
public sealed class DungeonRouteQueries(PulseDbContext db, IMemoryCache cache) : IDungeonRouteQueries
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public async Task<IReadOnlyList<SeasonDungeon>> GetCurrentAsync(CancellationToken ct = default) =>
        await cache.GetOrCreateAsync("season-dungeons", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            var season = await db.SeasonDungeons.AsNoTracking()
                .OrderByDescending(d => d.FetchedAtUtc)
                .Select(d => d.Season)
                .FirstOrDefaultAsync(ct);

            return (IReadOnlyList<SeasonDungeon>)await db.SeasonDungeons.AsNoTracking()
                .Where(d => d.Season == season)
                .OrderBy(d => d.Name)
                .ToListAsync(ct);
        }) ?? [];
}
