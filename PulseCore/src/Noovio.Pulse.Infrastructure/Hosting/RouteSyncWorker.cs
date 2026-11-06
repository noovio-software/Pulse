using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Noovio.Pulse.Application.Abstractions;
using Noovio.Pulse.Application.Routes;
using Noovio.Pulse.Infrastructure.KeystoneGuru;

namespace Noovio.Pulse.Infrastructure.Hosting;

/// <summary>
/// Synchronise les routes à intervalle fixe, à part de l'ingestion : elles ne dépendent pas du classement, et une
/// panne de l'une n'arrête pas l'autre. L'échéance repart de la dernière synchronisation en base, pour qu'un
/// redémarrage du worker (chaque déploiement) ne relise pas tout Keystone.guru.
/// </summary>
public sealed class RouteSyncWorker(
    IServiceScopeFactory scopes,
    IOptions<RouteSyncOptions> options,
    IOptions<KeystoneGuruOptions> credentials,
    TimeProvider time,
    ILogger<RouteSyncWorker> logger) : BackgroundService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!credentials.Value.IsConfigured)
        {
            return;
        }

        var wait = await TimeUntilNextSyncAsync(stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, time, stoppingToken);
            }

            wait = await SyncAsync(stoppingToken) ? options.Value.Interval : RetryDelay;
        }
    }

    private async Task<TimeSpan> TimeUntilNextSyncAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var last = await scope.ServiceProvider.GetRequiredService<IDungeonRouteStore>().GetLastFetchedAsync(ct);
        return last is { } at ? at + options.Value.Interval - time.GetUtcNow() : TimeSpan.Zero;
    }

    private async Task<bool> SyncAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var report = await scope.ServiceProvider.GetRequiredService<RouteSyncService>().RunAsync(ct);
            logger.LogInformation("Routes {Season} : {Updated} donjons à jour, {Failed} en échec", report.Season, report.Updated, report.Failed);
            return report.Updated > 0;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // Une synchronisation ratée ne doit pas arrêter le worker, et les routes en base restent affichées.
            logger.LogError(ex, "Synchronisation des routes en échec");
            return false;
        }
    }
}
