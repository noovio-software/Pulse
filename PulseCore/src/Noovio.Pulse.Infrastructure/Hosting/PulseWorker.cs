using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Noovio.Pulse.Application.Ingestion;
using Noovio.Pulse.Application.Routes;
using Noovio.Pulse.Domain.Roles;
using Noovio.Pulse.Infrastructure.Blizzard;
using Noovio.Pulse.Infrastructure.KeystoneGuru;

namespace Noovio.Pulse.Infrastructure.Hosting;

/// <summary>
/// Travail de fond d'une application Pulse : ingestion à intervalle fixe et synchronisation des routes, dans le conteneur
/// du site (mêmes services, même base) ; ou, en ligne de commande, un recalcul des snapshots ou une synchronisation unique.
/// </summary>
public static class PulseWorker
{
    public static IServiceCollection AddPulseWorkers(this IServiceCollection services, PulseApp app)
    {
        services.AddHostedService<MetaIngestionWorker>();
        if (app.HasDungeonRoutes)
        {
            services.AddHostedService<RouteSyncWorker>();
        }
        return services;
    }

    /// <summary>Signale au démarrage les identifiants manquants, sans lesquels l'ingestion ou les routes tournent à vide.</summary>
    public static void LogConfigurationWarnings(IServiceProvider services, PulseApp app)
    {
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(PulseWorker));

        // Sans identifiants Keystone.guru, l'API répond 401 : le worker des routes ne fait rien.
        if (app.HasDungeonRoutes && !services.GetRequiredService<IOptions<KeystoneGuruOptions>>().Value.IsConfigured)
        {
            logger.LogWarning("{App} : KeystoneGuru:Email / KeystoneGuru:Password manquants, les routes de donjons ne seront pas synchronisées.", app.Name);
        }

        // Sans clé, aucun profil ne se résout : l'ingestion tournerait à vide.
        if (!services.GetRequiredService<IOptions<BlizzardOptions>>().Value.IsConfigured)
        {
            logger.LogError("{App} : Blizzard:ClientId / Blizzard:ClientSecret manquants, aucune donnée ne sera ingérée.", app.Name);
        }
    }

    /// <summary><c>--rebuild-snapshots</c> : recalcule les snapshots depuis les profils en base (pas d'appel aux API).</summary>
    public static async Task RebuildSnapshotsAsync(IServiceProvider services, CancellationToken ct = default)
    {
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(PulseWorker));
        await using var scope = services.CreateAsyncScope();
        var report = await scope.ServiceProvider.GetRequiredService<MetaIngestionService>().RebuildSnapshotsAsync(ct);
        logger.LogInformation("Snapshots {Season} recalculés en {Duration} : {Specs}", report.Season, report.Duration,
            string.Join(", ", report.Specs.Select(s => $"{s.Spec}={s.Profiles}")));
    }

    /// <summary><c>--sync-routes</c> : synchronise les routes de donjons tout de suite. Faux si rien n'a pu être mis à jour.</summary>
    public static async Task<bool> SyncRoutesAsync(IServiceProvider services, PulseApp app, CancellationToken ct = default)
    {
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(PulseWorker));
        if (!app.HasDungeonRoutes || !services.GetRequiredService<IOptions<KeystoneGuruOptions>>().Value.IsConfigured)
        {
            logger.LogError("--sync-routes : routes désactivées pour {App}, ou identifiants Keystone.guru manquants.", app.Name);
            return false;
        }

        await using var scope = services.CreateAsyncScope();
        var report = await scope.ServiceProvider.GetRequiredService<RouteSyncService>().RunAsync(ct);
        logger.LogInformation("Routes {Season} : {Updated} donjons à jour, {Failed} en échec", report.Season, report.Updated, report.Failed);
        return report.Updated > 0;
    }
}
