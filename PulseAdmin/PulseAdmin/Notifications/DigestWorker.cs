using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PulseAdmin.Stats;

namespace PulseAdmin.Notifications;

public static class DigestText
{
    /// <summary>Le texte d'une notification : court, lisible sur l'écran verrouillé.</summary>
    public static PushMessage Build(UsageDigest digest, string label)
    {
        var title = $"PulseMetrics · {label}";
        if (digest.Benches == 0)
        {
            return new PushMessage(title, "Aucun bench sur la période.", "/");
        }

        var body = $"{Plural(digest.Benches, "bench", "benchs")} · {Plural(digest.Users, "utilisateur", "utilisateurs")}\n{digest.Names(6)}";
        return new PushMessage(title, body, "/");
    }

    public static string Plural(int count, string one, string many) => $"{count} {(count > 1 ? many : one)}";
}

/// <summary>
/// Envoie le récap de la période écoulée à chaque appareil abonné, aux heures de <see cref="NotificationOptions.Times"/>.
/// Chaque appareil retient la dernière période reçue : un redémarrage ne renvoie rien, un échec est retenté la minute suivante.
/// </summary>
public sealed class DigestWorker(
    IServiceScopeFactory scopes,
    IOptions<NotificationOptions> options,
    TimeProvider time,
    ILogger<DigestWorker> logger) : BackgroundService
{
    /// <summary>Un service arrêté à 12h ne doit pas envoyer « 7h → 12h » à 17h : passé ce délai, le récap est sauté.</summary>
    private static readonly TimeSpan CatchUpWindow = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.IsConfigured)
        {
            logger.LogInformation("Notifications:PublicKey/PrivateKey absentes : récaps désactivés");
            return;
        }

        var schedule = options.Value.Schedule();
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1), time);
        do
        {
            try
            {
                await TickAsync(schedule, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Une erreur (base indisponible) ne doit pas arrêter la boucle : la minute suivante réessaie.
                logger.LogError(ex, "Tour des récaps en échec");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task TickAsync(DigestSchedule schedule, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var (start, end) = schedule.LatestPeriod(now);
        if (now - end > CatchUpWindow)
        {
            return;
        }

        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AdminDbContext>();
        var due = await db.PushDevices.Where(d => d.LastDigestUtc == null || d.LastDigestUtc < end).ToListAsync(ct);
        if (due.Count == 0)
        {
            return;
        }

        var digest = await scope.ServiceProvider.GetRequiredService<UsageQueries>().GetDigestAsync(start, end, ct);
        var message = DigestText.Build(digest, schedule.Label(start, end));
        var sender = scope.ServiceProvider.GetRequiredService<PushSender>();

        var sent = 0;
        foreach (var device in due)
        {
            if (await sender.SendAsync(device, message, ct))
            {
                device.LastDigestUtc = end;
                sent++;
            }
        }

        // Seuls les appareils servis sont modifiés : un appareil supprimé entre-temps reste inchangé et n'est pas réécrit.
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Récap {Label} : {Benches} benchs, envoyé à {Sent}/{Due} appareil(s)", schedule.Label(start, end), digest.Benches, sent, due.Count);
    }
}
