using Microsoft.Extensions.Options;
using Noovio.FrontPulse.Application.Abstractions;
using Noovio.FrontPulse.Application.Ingestion;

namespace Noovio.FrontPulse.Worker;

/// <summary>
/// Relance l'ingestion complète à intervalle fixe (6 h par défaut), et dès qu'une demande forcée est déposée
/// par le site (page /admin/ingestion ou API). Une exécution à la fois : les demandes déposées pendant
/// une ingestion sont regroupées et servies juste après.
/// </summary>
public sealed class MetaIngestionWorker(
    IServiceScopeFactory scopes,
    IOptions<IngestionOptions> options,
    TimeProvider time,
    ILogger<MetaIngestionWorker> logger) : BackgroundService
{
    /// <summary>Fréquence de relève des demandes forcées.</summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using (var scope = scopes.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IIngestionRequestStore>().FailInterruptedAsync(stoppingToken);
        }

        var nextScheduled = options.Value.RunOnStartup ? time.GetUtcNow() : time.GetUtcNow() + options.Value.Interval;

        using var timer = new PeriodicTimer(PollInterval);
        do
        {
            var forced = await StartPendingRequestsAsync(stoppingToken);
            if (forced.Count == 0 && time.GetUtcNow() < nextScheduled)
            {
                continue;
            }

            if (forced.Count > 0)
            {
                logger.LogInformation("Ingestion forcée par {Requesters}", string.Join(", ", forced.Select(r => r.RequestedBy).Distinct()));
            }

            var (succeeded, message) = await RunOnceAsync(stoppingToken);
            nextScheduled = time.GetUtcNow() + options.Value.Interval;
            if (forced.Count > 0)
            {
                await CompleteAsync(forced.Select(r => r.Id).ToList(), succeeded, message, stoppingToken);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task<(bool Succeeded, string Message)> RunOnceAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var report = await scope.ServiceProvider.GetRequiredService<TankMetaIngestionService>().RunAsync(ct);
            var summary = $"{report.Season} en {report.Duration:hh\\:mm\\:ss} : " +
                          string.Join(", ", report.Specs.Select(s => $"{s.Spec}={s.Profiles}/{s.Candidates}"));
            logger.LogInformation("Ingestion terminée : {Summary}", summary);
            return (true, summary);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // Une ingestion ratée ne doit pas arrêter le worker : la suivante repartira de zéro.
            // Seul l'arrêt du service interrompt la boucle (un timeout HttpClient est aussi une OperationCanceledException).
            logger.LogError(ex, "Ingestion en échec");
            return (false, ex.Message);
        }
    }

    private async Task<IReadOnlyList<Domain.Entities.IngestionRequest>> StartPendingRequestsAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IIngestionRequestStore>().StartPendingAsync(ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // Base momentanément indisponible : le calendrier continue, la demande sera relevée au passage suivant.
            logger.LogWarning(ex, "Lecture des demandes d'ingestion impossible");
            return [];
        }
    }

    private async Task CompleteAsync(IReadOnlyList<long> ids, bool succeeded, string message, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IIngestionRequestStore>().CompleteAsync(ids, succeeded, message, ct);
    }
}
