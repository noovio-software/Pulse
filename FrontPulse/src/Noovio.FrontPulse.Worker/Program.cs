using Microsoft.Extensions.Options;
using Noovio.FrontPulse.Application;
using Noovio.FrontPulse.Application.Ingestion;
using Noovio.FrontPulse.Infrastructure;
using Noovio.FrontPulse.Infrastructure.Blizzard;
using Noovio.FrontPulse.Worker;

// --rebuild-snapshots : recalcule les snapshots depuis les profils en base, puis s'arrête (pas de cron).
var rebuildOnly = args.Contains("--rebuild-snapshots");

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddFrontPulseApplication(builder.Configuration);
builder.Services.AddFrontPulseInfrastructure(builder.Configuration);
if (!rebuildOnly)
{
    builder.Services.AddHostedService<MetaIngestionWorker>();
}

var host = builder.Build();
var logger = host.Services.GetRequiredService<ILogger<Program>>();

if (!host.Services.GetRequiredService<IOptions<BlizzardOptions>>().Value.IsConfigured)
{
    // Sans clé, aucun profil ne se résout : l'ingestion tournerait à vide.
    logger.LogError("Blizzard:ClientId / Blizzard:ClientSecret manquants : aucune donnée ne sera ingérée.");
}

await host.Services.MigrateFrontPulseDatabaseAsync();

if (rebuildOnly)
{
    await using var scope = host.Services.CreateAsyncScope();
    var report = await scope.ServiceProvider.GetRequiredService<TankMetaIngestionService>().RebuildSnapshotsAsync(CancellationToken.None);
    logger.LogInformation("Snapshots {Season} recalculés en {Duration} : {Specs}", report.Season, report.Duration,
        string.Join(", ", report.Specs.Select(s => $"{s.Spec}={s.Profiles}")));
    return;
}

await host.RunAsync();
