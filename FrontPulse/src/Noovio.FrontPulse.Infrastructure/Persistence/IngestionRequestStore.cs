using Microsoft.EntityFrameworkCore;
using Noovio.FrontPulse.Application.Abstractions;
using Noovio.FrontPulse.Domain.Entities;

namespace Noovio.FrontPulse.Infrastructure.Persistence;

public sealed class IngestionRequestStore(FrontPulseDbContext db, TimeProvider time) : IIngestionRequestStore
{
    public async Task<IngestionRequest> RequestAsync(string requestedBy, CancellationToken ct)
    {
        if (await db.IngestionRequests.AsNoTracking().FirstOrDefaultAsync(r => r.Status == IngestionRequestStatus.Pending, ct) is { } pending)
        {
            return pending;
        }

        var request = new IngestionRequest
        {
            RequestedAtUtc = time.GetUtcNow(),
            RequestedBy = requestedBy,
            Status = IngestionRequestStatus.Pending
        };
        db.IngestionRequests.Add(request);
        await db.SaveChangesAsync(ct);
        return request;
    }

    public async Task<IReadOnlyList<IngestionRequest>> StartPendingAsync(CancellationToken ct)
    {
        var pending = await db.IngestionRequests.Where(r => r.Status == IngestionRequestStatus.Pending).ToListAsync(ct);
        var now = time.GetUtcNow();
        foreach (var request in pending)
        {
            request.Status = IngestionRequestStatus.Running;
            request.StartedAtUtc = now;
        }

        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
        return pending;
    }

    public async Task CompleteAsync(IReadOnlyList<long> ids, bool succeeded, string message, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var status = succeeded ? IngestionRequestStatus.Succeeded : IngestionRequestStatus.Failed;
        await db.IngestionRequests
            .Where(r => ids.Contains(r.Id))
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, status)
                .SetProperty(r => r.CompletedAtUtc, now)
                .SetProperty(r => r.Message, message), ct);
    }

    public async Task FailInterruptedAsync(CancellationToken ct) =>
        await db.IngestionRequests
            .Where(r => r.Status == IngestionRequestStatus.Running)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, IngestionRequestStatus.Failed)
                .SetProperty(r => r.CompletedAtUtc, time.GetUtcNow())
                .SetProperty(r => r.Message, "Interrompue par un redémarrage du worker"), ct);

    public async Task<IReadOnlyList<IngestionRequest>> GetRecentAsync(int count, CancellationToken ct) =>
        await db.IngestionRequests.AsNoTracking()
            .OrderByDescending(r => r.RequestedAtUtc)
            .Take(count)
            .ToListAsync(ct);
}
