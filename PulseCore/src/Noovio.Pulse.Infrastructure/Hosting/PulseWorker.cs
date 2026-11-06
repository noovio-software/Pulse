using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Noovio.Pulse.Application;
using Noovio.Pulse.Application.Ingestion;
using Noovio.Pulse.Application.Routes;
using Noovio.Pulse.Domain.Roles;
using Noovio.Pulse.Infrastructure.Blizzard;
using Noovio.Pulse.Infrastructure.KeystoneGuru;
using Noovio.Pulse.Infrastructure.Persistence;

namespace Noovio.Pulse.Infrastructure.Hosting;

/// <summary>Hôte du worker d'une application Pulse : ingestion à intervalle fixe, ou recalcul des snapshots seul.</summary>
public static class PulseWorker
{
    public static async Task RunAsync<TContext>(string[] args, PulseApp app) where TContext : PulseDbContext
    {
        // --rebuild-snapshots : recalcule les snapshots depuis les profils en base, puis s'arrête (pas de cron).
        var rebuildOnly = args.Contains("--rebuild-snapshots");

        // --sync-routes : synchronise les routes de donjons tout de suite, puis s'arrête.
        var routesOnly = args.Contains("--sync-routes");

        var builder = Host.CreateApplicationBuilder(args);

        builder.Services.AddPulseApplication(builder.Configuration, app);
        builder.Services.AddPulseInfrastructure<TContext>(builder.Configuration, app);
        if (!rebuildOnly && !routesOnly)
        {
            builder.Services.AddHostedService<MetaIngestionWorker>();
            if (app.HasDungeonRoutes)
            {
                builder.Services.AddHostedService<RouteSyncWorker>();
            }
        }

        var host = builder.Build();
        var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(PulseWorker));

        // Sans identifiants Keystone.guru, l'API répond 401 : le worker des routes ne fait rien.
        var syncRoutes = app.HasDungeonRoutes && host.Services.GetRequiredService<IOptions<KeystoneGuruOptions>>().Value.IsConfigured;
        if (app.HasDungeonRoutes && !syncRoutes)
        {
            logger.LogWarning("KeystoneGuru:Email / KeystoneGuru:Password manquants : les routes de donjons ne seront pas synchronisées.");
        }

        if (!host.Services.GetRequiredService<IOptions<BlizzardOptions>>().Value.IsConfigured)
        {
            // Sans clé, aucun profil ne se résout : l'ingestion tournerait à vide.
            logger.LogError("Blizzard:ClientId / Blizzard:ClientSecret manquants : aucune donnée ne sera ingérée.");
        }

        await host.Services.MigratePulseDatabaseAsync();

        if (rebuildOnly)
        {
            await using var scope = host.Services.CreateAsyncScope();
            var report = await scope.ServiceProvider.GetRequiredService<MetaIngestionService>().RebuildSnapshotsAsync(CancellationToken.None);
            logger.LogInformation("Snapshots {Season} recalculés en {Duration} : {Specs}", report.Season, report.Duration,
                string.Join(", ", report.Specs.Select(s => $"{s.Spec}={s.Profiles}")));
            return;
        }

        if (routesOnly)
        {
            if (!syncRoutes)
            {
                logger.LogError("--sync-routes : routes désactivées pour {App}, ou identifiants Keystone.guru manquants.", app.Name);
                Environment.ExitCode = 1;
                return;
            }

            await using var scope = host.Services.CreateAsyncScope();
            var report = await scope.ServiceProvider.GetRequiredService<RouteSyncService>().RunAsync(CancellationToken.None);
            logger.LogInformation("Routes {Season} : {Updated} donjons à jour, {Failed} en échec", report.Season, report.Updated, report.Failed);
            Environment.ExitCode = report.Updated > 0 ? 0 : 1;
            return;
        }

        await host.RunAsync();
    }
}
