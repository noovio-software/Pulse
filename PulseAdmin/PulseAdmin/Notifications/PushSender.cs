using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WebPush;

namespace PulseAdmin.Notifications;

public sealed class NotificationOptions
{
    public const string SectionName = "Notifications";

    /// <summary>Clé VAPID publique, donnée au navigateur : pas un secret. <c>PulseAdmin --vapid-keys</c> génère la paire.</summary>
    public string PublicKey { get; set; } = "";

    /// <summary>Clé VAPID privée : secret, dans l'environnement en production.</summary>
    public string PrivateKey { get; set; } = "";

    /// <summary>Identifie l'expéditeur auprès des services de push (une adresse https: ou mailto:).</summary>
    public string Subject { get; set; } = "https://admin.pulsemetrics.fr";

    /// <summary>
    /// Heures des récaps, heure de Paris ; vide : 7h, 12h, 18h, 21h. (Pas de valeur par défaut dans la liste :
    /// la configuration s'y ajouterait au lieu de la remplacer.)
    /// </summary>
    public List<string> Times { get; set; } = [];

    private static readonly string[] DefaultTimes = ["07:00", "12:00", "18:00", "21:00"];

    /// <summary>
    /// Durée de vie d'un push non distribué (appareil éteint) : au-delà, le récap n'a plus d'intérêt,
    /// le suivant le remplace. La bibliothèque garderait sinon le message quatre semaines.
    /// </summary>
    public int TtlSeconds { get; set; } = 4 * 3600;

    public bool IsConfigured => PublicKey.Length > 0 && PrivateKey.Length > 0;

    public DigestSchedule Schedule() => new((Times.Count > 0 ? Times : [.. DefaultTimes]).Select(t => TimeOnly.Parse(t, System.Globalization.CultureInfo.InvariantCulture)), Stats.StatsFilter.Zone);

    /// <summary>« 7h, 12h, 18h, 21h ».</summary>
    public string TimesLabel => string.Join(", ", Schedule().Times.Select(t => t.Minute == 0 ? $"{t.Hour}h" : $"{t.Hour}h{t.Minute:00}"));
}

public sealed record PushMessage(string Title, string Body, string Url, string Tag = "pulseadmin-digest");

/// <summary>Envoi Web Push (VAPID), repris de Nowtilus : urgence haute et TTL court, appareil supprimé quand le navigateur l'a oublié.</summary>
public sealed class PushSender(AdminDbContext db, IOptions<NotificationOptions> options, ILogger<PushSender> logger)
{
    private static readonly WebPushClient Client = new();

    /// <returns>false si rien n'est parti (appareil oublié et supprimé, ou échec passager à retenter).</returns>
    public async Task<bool> SendAsync(PushDevice device, PushMessage message, CancellationToken ct)
    {
        var settings = options.Value;
        if (!settings.IsConfigured)
        {
            logger.LogWarning("Clés VAPID absentes : notification non envoyée");
            return false;
        }

        var send = new Dictionary<string, object>
        {
            ["vapidDetails"] = new VapidDetails(settings.Subject, settings.PublicKey, settings.PrivateKey),
            ["TTL"] = settings.TtlSeconds,
            // Sans urgence haute, Android garde le message jusqu'au prochain réveil naturel de l'appareil.
            ["headers"] = new Dictionary<string, object> { ["Urgency"] = "high" },
        };

        try
        {
            await Client.SendNotificationAsync(
                new PushSubscription(device.Endpoint, device.P256dh, device.Auth),
                JsonSerializer.Serialize(message, JsonSerializerOptions.Web),
                send,
                ct);
            return true;
        }
        catch (WebPushException ex) when (ex.StatusCode is HttpStatusCode.Gone or HttpStatusCode.NotFound)
        {
            // Le service de push a définitivement oublié l'appareil (désabonné, application désinstallée).
            logger.LogInformation("Appareil {Id} désabonné par le navigateur ({Status}) : supprimé", device.Id, (int)ex.StatusCode);
            await db.PushDevices.Where(d => d.Id == device.Id).ExecuteDeleteAsync(ct);
            return false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning("Notification vers l'appareil {Id} en échec : {ExceptionType}", device.Id, ex.GetType().Name);
            return false;
        }
    }
}
