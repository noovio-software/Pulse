using Noovio.Pulse.Domain.Roles;
using Noovio.Pulse.Infrastructure;
using Noovio.Pulse.Infrastructure.Hosting;
using Noovio.Pulse.Infrastructure.Http;
using Noovio.Pulse.Web;
using PulseMetrics.Alt;
using PulseMetrics.Burst;
using PulseMetrics.Burst.Persistence;
using PulseMetrics.Front;
using PulseMetrics.Front.Persistence;
using PulseMetrics.Hosting;
using PulseMetrics.Landing;
using PulseMetrics.Life;
using PulseMetrics.Life.Persistence;

// Un seul processus, un seul port : la page d'accueil à la racine, chaque outil sous son préfixe.
//   --no-workers         : sites seuls, sans ingestion ni synchronisation des routes (développement).
//   --rebuild-snapshots  : recalcule les snapshots de Front, Life et Burst depuis les profils en base, puis s'arrête.
//   --sync-routes        : synchronise les routes de donjons (Front), puis s'arrête.
string[] flags = ["--no-workers", "--rebuild-snapshots", "--sync-routes"];
var hostArgs = args.Except(flags).ToArray();
var rebuildSnapshots = args.Contains("--rebuild-snapshots");
var syncRoutes = args.Contains("--sync-routes");
var workers = !args.Contains("--no-workers") && !rebuildSnapshots && !syncRoutes;

(string Prefix, PulseApp Pulse, Func<WebApplicationBuilder, PulseApp, bool, WebApplication> Build)[] pulseTools =
[
    ("/front", FrontPulseApp.Definition, PulseSite.Build<FrontPulseDbContext>),
    ("/life", LifePulseApp.Definition, PulseSite.Build<LifePulseDbContext>),
    ("/burst", BurstPulseApp.Definition, PulseSite.Build<BurstPulseDbContext>),
];

await using var tools = new Tools(hostArgs);
var pulseApps = new List<(PulseApp Pulse, WebApplication App)>();
foreach (var (prefix, pulse, build) in pulseTools)
{
    var app = await tools.AddAsync(prefix, builder =>
    {
        // Un seul client Blizzard et une seule adresse IP pour les trois outils : leurs limiteurs de débit sont partagés
        // (ceux du premier outil), sinon les quotas horaires et par seconde seraient dépassés à trois.
        if (pulseApps.Count > 0)
        {
            var first = pulseApps[0].App.Services;
            builder.Services.AddSingleton(first.GetRequiredService<BlizzardRateLimiter>());
            builder.Services.AddSingleton(first.GetRequiredService<RaiderIoRateLimiter>());
        }
        return build(builder, pulse, workers);
    });
    // Avant tout démarrage : les sites et les workers lisent un schéma à jour.
    await app.Services.MigratePulseDatabaseAsync();
    pulseApps.Add((pulse, app));
}

if (rebuildSnapshots || syncRoutes)
{
    var failed = false;
    foreach (var (pulse, app) in pulseApps)
    {
        if (rebuildSnapshots)
        {
            await PulseWorker.RebuildSnapshotsAsync(app.Services);
        }
        if (syncRoutes && pulse.HasDungeonRoutes)
        {
            failed |= !await PulseWorker.SyncRoutesAsync(app.Services, pulse);
        }
    }
    return failed ? 1 : 0;
}

await tools.AddAsync("/alt", AltSite.Build);
await tools.AddAsync("", LandingSite.Build);

var host = WebApplication.CreateBuilder(hostArgs).Build();
host.Run(tools.DispatchAsync);

await tools.StartAsync();
try
{
    // Ctrl+C / SIGTERM arrêtent l'hôte, qui ferme les connexions avant que les outils et leurs workers ne s'arrêtent.
    await host.RunAsync();
}
finally
{
    await tools.StopAsync();
}
return 0;
