using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Noovio.Pulse.Application;
using Noovio.Pulse.Application.Ingestion;
using Noovio.Pulse.Domain.Roles;
using Noovio.Pulse.Infrastructure.Blizzard;
using Noovio.Pulse.Infrastructure.Persistence;

namespace Noovio.Pulse.Infrastructure.Hosting;

/// <summary>Hôte du worker d'une application Pulse : ingestion à intervalle fixe, ou recalcul des snapshots seul.</summary>
public static class PulseWorker
{
    public static async Task RunAsync<TContext>(string[] args, PulseApp app) where TContext : PulseDbContext
    {
        // --rebuild-snapshots : recalcule les snapshots depuis les profils en base, puis s'arrête (pas de cron).
        var rebuildOnly = args.Contains("--rebuild-snapshots");

        var builder = Host.CreateApplicationBuilder(args);

        builder.Services.AddPulseApplication(builder.Configuration, app);
        builder.Services.AddPulseInfrastructure<TContext>(builder.Configuration, app);
        if (!rebuildOnly)
        {
            builder.Services.AddHostedService<MetaIngestionWorker>();
        }

        var host = builder.Build();
        var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(PulseWorker));

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

        await host.RunAsync();
    }
}
