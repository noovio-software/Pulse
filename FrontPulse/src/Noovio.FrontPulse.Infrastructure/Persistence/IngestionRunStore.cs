using Microsoft.EntityFrameworkCore;
using Noovio.FrontPulse.Application.Abstractions;
using Noovio.FrontPulse.Application.Ingestion;
using Noovio.FrontPulse.Domain.Entities;

namespace Noovio.FrontPulse.Infrastructure.Persistence;

public sealed class IngestionRunStore(FrontPulseDbContext db, TimeProvider time) : IIngestionRunStore
{
    public async Task<IngestionRun> StartAsync(bool forced, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var run = new IngestionRun
        {
            StartedAtUtc = now,
            UpdatedAtUtc = now,
            Forced = forced,
            Status = IngestionRunStatus.Running,
            Phase = IngestionPhase.Leaderboard
        };
        db.IngestionRuns.Add(run);
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
        return run;
    }

    public async Task ReportAsync(long id, IngestionProgress progress, bool forced, CancellationToken ct) =>
        await db.IngestionRuns
            .Where(r => r.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.UpdatedAtUtc, time.GetUtcNow())
                .SetProperty(r => r.Forced, r => r.Forced || forced)
                .SetProperty(r => r.Phase, progress.Phase)
                .SetProperty(r => r.LeaderboardQueriesDone, progress.LeaderboardQueriesDone)
                .SetProperty(r => r.LeaderboardQueries, progress.LeaderboardQueries)
                .SetProperty(r => r.ProfilesStartedAtUtc, progress.ProfilesStartedAtUtc)
                .SetProperty(r => r.CurrentSpec, progress.CurrentSpec)
                .SetProperty(r => r.SpecsDone, progress.SpecsDone)
                .SetProperty(r => r.Candidates, progress.Candidates)
                .SetProperty(r => r.ProfilesRead, progress.ProfilesRead), ct);

    public async Task CompleteAsync(long id, bool succeeded, string message, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        await db.IngestionRuns
            .Where(r => r.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, succeeded ? IngestionRunStatus.Succeeded : IngestionRunStatus.Failed)
                .SetProperty(r => r.UpdatedAtUtc, now)
                .SetProperty(r => r.CompletedAtUtc, now)
                .SetProperty(r => r.Message, message), ct);
    }

    public async Task<IReadOnlyList<IngestionRun>> GetRecentAsync(int count, CancellationToken ct) =>
        await db.IngestionRuns.AsNoTracking()
            .OrderByDescending(r => r.StartedAtUtc)
            .Take(count)
            .ToListAsync(ct);

    public async Task FailInterruptedAsync(CancellationToken ct) =>
        await db.IngestionRuns
            .Where(r => r.Status == IngestionRunStatus.Running)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, IngestionRunStatus.Failed)
                .SetProperty(r => r.CompletedAtUtc, time.GetUtcNow())
                .SetProperty(r => r.Message, "Interrompue par un redémarrage du worker"), ct);
}
