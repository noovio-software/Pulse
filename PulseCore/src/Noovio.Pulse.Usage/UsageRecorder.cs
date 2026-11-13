using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Noovio.Pulse.Usage;

/// <summary>Enregistre un bench. Ne bloque jamais et n'échoue jamais : les statistiques ne doivent pas ralentir ni casser une analyse.</summary>
public interface IUsageRecorder
{
    void Record(BenchEvent bench);
}

/// <summary>Sans chaîne de connexion <c>PulseUsage</c> (développement) : rien n'est enregistré.</summary>
internal sealed class NullUsageRecorder : IUsageRecorder
{
    public void Record(BenchEvent bench)
    {
    }
}

/// <summary>
/// File en mémoire vidée par <see cref="UsageWriter"/>. Bornée : si la base ne suit plus, les plus anciens
/// benchs en attente sont perdus plutôt que la mémoire du site.
/// </summary>
internal sealed class ChannelUsageRecorder : IUsageRecorder
{
    private readonly Channel<BenchEvent> _channel = Channel.CreateBounded<BenchEvent>(
        new BoundedChannelOptions(2000) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });

    public ChannelReader<BenchEvent> Reader => _channel.Reader;

    public void Record(BenchEvent bench) => _channel.Writer.TryWrite(bench);
}

/// <summary>Écrit les benchs en base par lots, et applique au démarrage les migrations de la base d'usage.</summary>
internal sealed class UsageWriter(ChannelUsageRecorder recorder, IServiceProvider services, ILogger<UsageWriter> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await services.MigrateUsageDatabaseAsync(stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Les écritures seront quand même tentées : PulseAdmin a pu appliquer les migrations entre-temps.
            logger.LogWarning(ex, "Base d'usage indisponible au démarrage");
        }

        var batch = new List<BenchEvent>();
        while (await recorder.Reader.WaitToReadAsync(stoppingToken))
        {
            while (batch.Count < 100 && recorder.Reader.TryRead(out var bench))
            {
                batch.Add(bench);
            }

            try
            {
                await using var scope = services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<UsageDbContext>();
                db.BenchEvents.AddRange(batch);
                await db.SaveChangesAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "{Count} bench(s) non enregistré(s) dans la base d'usage", batch.Count);
            }

            batch.Clear();
        }
    }
}
