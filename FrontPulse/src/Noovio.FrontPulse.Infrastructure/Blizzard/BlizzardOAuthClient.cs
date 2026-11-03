using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Noovio.FrontPulse.Infrastructure.RaiderIo;

namespace Noovio.FrontPulse.Infrastructure.Blizzard;

public sealed class BlizzardOptions
{
    public const string SectionName = "Blizzard";

    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }

    /// <summary>
    /// Langue des noms affichés (objets, talents, arbres héroïques, embellissements).
    /// Les descriptions servent aussi au classement défensif/offensif des bijoux (mots-clés FR et EN).
    /// </summary>
    public string Locale { get; set; } = "fr_FR";

    /// <summary>Région interrogée pour les données statiques (icônes d'objets), identiques partout.</summary>
    public string StaticRegion { get; set; } = "us";

    /// <summary>Un peu sous les quotas officiels (100/s, 36 000/h) : le jeton OAuth et les relances comptent aussi.</summary>
    public int RequestsPerSecond { get; set; } = 90;
    public int RequestsPerHour { get; set; } = 35_000;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}

/// <summary>
/// Jeton client_credentials Battle.net. Gardé en mémoire et en cache distribué (Redis) :
/// le site et le worker, ou plusieurs redémarrages, partagent le même jeton tant qu'il est valide.
/// </summary>
public sealed class BlizzardOAuthClient(HttpClient http, IDistributedCache cache, IOptions<BlizzardOptions> options, TimeProvider time)
{
    private const string CacheKey = "blizzard:token";
    private static readonly SemaphoreSlim TokenLock = new(1, 1);
    private static string? _token;
    private static DateTimeOffset _tokenExpiresAt;

    private readonly BlizzardOptions _options = options.Value;

    public async Task<string> GetTokenAsync(CancellationToken ct)
    {
        if (_token is not null && time.GetUtcNow() < _tokenExpiresAt)
        {
            return _token;
        }

        await TokenLock.WaitAsync(ct);
        try
        {
            if (_token is not null && time.GetUtcNow() < _tokenExpiresAt)
            {
                return _token;
            }

            if (await cache.GetStringAsync(CacheKey, ct) is { } cached)
            {
                // L'expiration exacte n'est connue que de Redis : on relit le cache toutes les 5 minutes.
                Remember(cached, TimeSpan.FromMinutes(5));
                return cached;
            }

            var token = await RequestTokenAsync(ct);
            var lifetime = TimeSpan.FromSeconds(Math.Max(60, token.ExpiresIn - 300));
            await cache.SetStringAsync(CacheKey, token.AccessToken, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = lifetime }, ct);
            Remember(token.AccessToken, lifetime);
            return token.AccessToken;
        }
        finally
        {
            TokenLock.Release();
        }
    }

    /// <summary>Après un 401 : le jeton a été révoqué ou a expiré plus tôt que prévu.</summary>
    public async Task InvalidateAsync(CancellationToken ct)
    {
        _token = null;
        await cache.RemoveAsync(CacheKey, ct);
    }

    private void Remember(string token, TimeSpan lifetime)
    {
        _token = token;
        _tokenExpiresAt = time.GetUtcNow() + lifetime;
    }

    private async Task<BzToken> RequestTokenAsync(CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://oauth.battle.net/token")
        {
            Content = new FormUrlEncodedContent([new("grant_type", "client_credentials")])
        };
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.ClientId}:{_options.ClientSecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);

        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        return await JsonSerializer.DeserializeAsync<BzToken>(stream, RaiderIoClient.JsonOptions, ct)
               ?? throw new InvalidOperationException("Jeton Battle.net vide.");
    }
}
