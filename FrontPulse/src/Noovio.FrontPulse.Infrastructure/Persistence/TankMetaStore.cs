using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Noovio.FrontPulse.Application.Abstractions;
using Noovio.FrontPulse.Domain;
using Noovio.FrontPulse.Domain.Entities;
using Noovio.FrontPulse.Domain.Enums;

namespace Noovio.FrontPulse.Infrastructure.Persistence;

public sealed class TankMetaStore(FrontPulseDbContext db) : ITankMetaStore
{
    public async Task ReplaceAsync(string season, TankSpec spec, IReadOnlyList<TankProfile> profiles, SpecMetaSnapshot snapshot, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        await db.TankProfiles.Where(p => p.Season == season && p.Spec == spec).ExecuteDeleteAsync(ct);
        await db.SpecMetas.Where(m => m.Season == season && m.Spec == spec).ExecuteDeleteAsync(ct);

        db.TankProfiles.AddRange(profiles);
        db.SpecMetas.Add(ToEntity(snapshot));

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        db.ChangeTracker.Clear();
    }

    public async Task<IReadOnlyList<TankProfile>> GetProfilesAsync(string season, TankSpec spec, CancellationToken ct) =>
        await db.TankProfiles.AsNoTracking()
            .Where(p => p.Season == season && p.Spec == spec)
            .OrderBy(p => p.Rank)
            .ToListAsync(ct);

    public async Task ReplaceSnapshotAsync(SpecMetaSnapshot snapshot, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.SpecMetas.Where(m => m.Season == snapshot.Season && m.Spec == snapshot.Spec).ExecuteDeleteAsync(ct);
        db.SpecMetas.Add(ToEntity(snapshot));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        db.ChangeTracker.Clear();
    }

    private static SpecMeta ToEntity(SpecMetaSnapshot snapshot) => new()
    {
        Season = snapshot.Season,
        Spec = snapshot.Spec,
        ComputedAtUtc = snapshot.ComputedAtUtc,
        SampleSize = snapshot.SampleSize,
        Snapshot = snapshot
    };
}

/// <summary>
/// Lecture des snapshots par le site. Ils ne changent qu'à chaque ingestion (6 h) :
/// quelques minutes de cache mémoire évitent de relire le jsonb à chaque circuit Blazor.
/// </summary>
public sealed class SpecMetaQueries(FrontPulseDbContext db, IMemoryCache cache) : ISpecMetaQueries
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public async Task<SpecMetaSnapshot?> GetLatestAsync(TankSpec spec, CancellationToken ct = default) =>
        await cache.GetOrCreateAsync($"spec-meta:{spec}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return await db.SpecMetas.AsNoTracking()
                .Where(m => m.Spec == spec)
                .OrderByDescending(m => m.ComputedAtUtc)
                .Select(m => m.Snapshot)
                .FirstOrDefaultAsync(ct);
        });

    public async Task<IReadOnlyList<SpecMetaSummary>> GetSummariesAsync(CancellationToken ct = default)
    {
        var summaries = new List<SpecMetaSummary>();
        foreach (var info in TankSpecs.All)
        {
            if (await GetLatestAsync(info.Spec, ct) is { } s)
            {
                var hero = s.HeroTrees.FirstOrDefault()?.Name;
                var heroIcon = s.TalentTree?.Sections.FirstOrDefault(x => x.Tree == TalentTree.Hero && x.Name == hero)?.IconUrl;
                summaries.Add(new SpecMetaSummary(s.Spec, s.Season, s.ComputedAtUtc, s.SampleSize, s.MedianItemLevel, s.MedianKeyLevel, hero, heroIcon));
            }
        }

        return summaries;
    }
}
