using System.Net;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Noovio.Pulse.Infrastructure.Blizzard;
using Noovio.Pulse.Infrastructure.RaiderIo;

namespace Noovio.Pulse.Infrastructure.Http;

/// <summary>
/// Limiteur partagé par toutes les instances d'un client HTTP (singleton) : les HttpClient typés
/// sont transitoires, mais le quota de l'API est global au processus.
/// Les requêtes en excès attendent leur tour dans la file au lieu d'échouer.
/// </summary>
public abstract class HttpRateLimiter(PartitionedRateLimiter<HttpRequestMessage> limiter) : IDisposable
{
    public PartitionedRateLimiter<HttpRequestMessage> Limiter { get; } = limiter;

    public void Dispose()
    {
        Limiter.Dispose();
        GC.SuppressFinalize(this);
    }

    protected static PartitionedRateLimiter<HttpRequestMessage> TokenBucket(int perSecond) =>
        PartitionedRateLimiter.Create<HttpRequestMessage, string>(_ => RateLimitPartition.GetTokenBucketLimiter("second", _ => new TokenBucketRateLimiterOptions
        {
            TokenLimit = perSecond,
            TokensPerPeriod = perSecond,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            QueueLimit = int.MaxValue,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true
        }));

    /// <summary>
    /// Fenêtre glissante plutôt qu'un seau : un seau plein au démarrage autoriserait
    /// une rafale de N requêtes en plus du débit nominal sur la première heure.
    /// </summary>
    protected static PartitionedRateLimiter<HttpRequestMessage> SlidingHour(int perHour) =>
        PartitionedRateLimiter.Create<HttpRequestMessage, string>(_ => RateLimitPartition.GetSlidingWindowLimiter("hour", _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = perHour,
            Window = TimeSpan.FromHours(1),
            SegmentsPerWindow = 60,
            QueueLimit = int.MaxValue,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true
        }));
}

/// <summary>Quotas Blizzard : 100 req/s et 36 000 req/h par client OAuth.</summary>
public sealed class BlizzardRateLimiter(IOptions<BlizzardOptions> options) : HttpRateLimiter(
    PartitionedRateLimiter.CreateChained(TokenBucket(options.Value.RequestsPerSecond), SlidingHour(options.Value.RequestsPerHour)));

public sealed class RaiderIoRateLimiter(IOptions<RaiderIoOptions> options) : HttpRateLimiter(TokenBucket(options.Value.RequestsPerSecond));

/// <summary>
/// Applique le limiteur avant chaque envoi, et respecte un 429 éventuel (Retry-After,
/// sinon attente exponentielle) avant de réessayer.
/// </summary>
public sealed class RateLimitingHandler<TLimiter>(TLimiter limiter, ILogger<TLimiter> logger) : DelegatingHandler
    where TLimiter : HttpRateLimiter
{
    private const int MaxRetries = 3;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            using (var lease = await limiter.Limiter.AcquireAsync(request, 1, cancellationToken))
            {
                if (!lease.IsAcquired)
                {
                    throw new HttpRequestException($"Quota local d'appels refusé pour {request.RequestUri?.Host}.");
                }
            }

            var response = await base.SendAsync(request, cancellationToken);
            if (response.StatusCode != HttpStatusCode.TooManyRequests || attempt >= MaxRetries)
            {
                return response;
            }

            var delay = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(Math.Pow(2, attempt + 1));
            logger.LogWarning("429 sur {Host}, nouvel essai dans {Delay}", request.RequestUri?.Host, delay);
            response.Dispose();
            await Task.Delay(delay, cancellationToken);
        }
    }
}
