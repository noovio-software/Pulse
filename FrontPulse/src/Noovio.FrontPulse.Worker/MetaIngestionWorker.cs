using Microsoft.Extensions.Options;
using Noovio.FrontPulse.Application.Abstractions;
using Noovio.FrontPulse.Application.Ingestion;

namespace Noovio.FrontPulse.Worker;

/// <summary>
/// Relance l'ingestion complète à intervalle fixe (6 h par défaut), et dès qu'une demande forcée est déposée
/// par le site (page /admin/ingestion ou API). Une exécution à la fois : une demande déposée pendant
/// une ingestion la rejoint aussitôt (elle passe « en cours » et se termine avec elle) plutôt que d'attendre
/// derrière une demi-heure pour en relancer une seconde.
/// </summary>
public sealed class MetaIngestionWorker(
    IServiceScopeFactory scopes,
    IOptions<IngestionOptions> options,
    TimeProvider time,
    ILogger<MetaIngestionWorker> logger) : BackgroundService
{
    /// <summary>Fréquence de relève des demandes forcées : une requête indexée, assez légère pour quelques secondes.</summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using (var scope = scopes.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IIngestionRequestStore>().FailInterruptedAsync(stoppingToken);
            await scope.ServiceProvider.GetRequiredService<IIngestionRunStore>().FailInterruptedAsync(stoppingToken);
        }

        var nextScheduled = options.Value.RunOnStartup ? time.GetUtcNow() : time.GetUtcNow() + options.Value.Interval;

        using var timer = new PeriodicTimer(PollInterval);
        do
        {
            var forced = (await StartPendingRequestsAsync(stoppingToken)).ToList();
            if (forced.Count == 0 && time.GetUtcNow() < nextScheduled)
            {
                continue;
            }

            if (forced.Count > 0)
            {
                logger.LogInformation("Ingestion forcée par {Requesters}", string.Join(", ", forced.Select(r => r.RequestedBy).Distinct()));
            }

            var progress = new IngestionProgress();
            var runId = await UseRunsAsync(async runs => (long?)(await runs.StartAsync(forced.Count > 0, stoppingToken)).Id);

            // Pendant l'exécution : avancement recopié en base, et les nouvelles demandes la rejoignent au fil de l'eau.
            // Task.Delay plutôt que le PeriodicTimer : il n'accepte qu'une attente à la fois, et celle de la boucle reprend après.
            var run = RunOnceAsync(progress, stoppingToken);
            while (await Task.WhenAny(run, Task.Delay(PollInterval, stoppingToken)) != run)
            {
                var joined = await StartPendingRequestsAsync(stoppingToken);
                if (joined.Count > 0)
                {
                    logger.LogInformation("Demande de {Requesters} rattachée à l'ingestion en cours", string.Join(", ", joined.Select(r => r.RequestedBy).Distinct()));
                    forced.AddRange(joined);
                }

                if (runId is { } id)
                {
                    await UseRunsAsync(async runs => { await runs.ReportAsync(id, progress, forced.Count > 0, stoppingToken); return true; });
                }
            }

            var (succeeded, message) = await run;
            nextScheduled = time.GetUtcNow() + options.Value.Interval;
            if (runId is { } done)
            {
                await UseRunsAsync(async runs =>
                {
                    await runs.ReportAsync(done, progress, forced.Count > 0, stoppingToken);
                    await runs.CompleteAsync(done, succeeded, message, stoppingToken);
                    return true;
                });
            }
            if (forced.Count > 0)
            {
                await CompleteAsync(forced.Select(r => r.Id).ToList(), succeeded, message, stoppingToken);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>
    /// Suivi des exécutions : une base momentanément indisponible ne doit pas arrêter l'ingestion,
    /// seule la page d'admin perd l'avancement le temps de la coupure.
    /// </summary>
    private async Task<T?> UseRunsAsync<T>(Func<IIngestionRunStore, Task<T>> action)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            return await action(scope.ServiceProvider.GetRequiredService<IIngestionRunStore>());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Suivi de l'ingestion impossible");
            return default;
        }
    }

    private async Task<(bool Succeeded, string Message)> RunOnceAsync(IngestionProgress progress, CancellationToken ct)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var report = await scope.ServiceProvider.GetRequiredService<TankMetaIngestionService>().RunAsync(ct, progress);
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
