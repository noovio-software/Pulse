using Microsoft.Extensions.Options;
using Noovio.FrontPulse.Application.Ingestion;

namespace Noovio.FrontPulse.Worker;

/// <summary>Relance l'ingestion complète à intervalle fixe (6 h par défaut), une exécution à la fois.</summary>
public sealed class MetaIngestionWorker(
    IServiceScopeFactory scopes,
    IOptions<IngestionOptions> options,
    ILogger<MetaIngestionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (options.Value.RunOnStartup)
        {
            await RunOnceAsync(stoppingToken);
        }

        using var timer = new PeriodicTimer(options.Value.Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunOnceAsync(stoppingToken);
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var report = await scope.ServiceProvider.GetRequiredService<TankMetaIngestionService>().RunAsync(ct);
            logger.LogInformation("Ingestion {Season} terminée en {Duration} : {Specs}", report.Season, report.Duration,
                string.Join(", ", report.Specs.Select(s => $"{s.Spec}={s.Profiles}/{s.Candidates}")));
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // Une ingestion ratée ne doit pas arrêter le worker : la suivante repartira de zéro.
            // Seul l'arrêt du service interrompt la boucle (un timeout HttpClient est aussi une OperationCanceledException).
            logger.LogError(ex, "Ingestion en échec");
        }
    }
}
