using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace AltPulse.Services.Blizzard;

public sealed class BlizzardOptions
{
    public const string SectionName = "Blizzard";

    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string Locale { get; set; } = "fr_FR";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}

/// <summary>
/// Client des Game Data / Profile APIs Blizzard (https://develop.battle.net).
/// Le secret reste côté serveur : le front WASM ne parle qu'à nos endpoints /api.
/// </summary>
public sealed class BlizzardClient(HttpClient http, IOptions<BlizzardOptions> options, ILogger<BlizzardClient> logger)
{
    private static readonly SemaphoreSlim TokenLock = new(1, 1);
    private static string? _token;
    private static DateTimeOffset _tokenExpiresAt;

    private readonly BlizzardOptions _options = options.Value;

    public bool IsConfigured => _options.IsConfigured;

    public Task<BzCharacterProfile?> GetProfileAsync(string region, string realmSlug, string name, CancellationToken ct) =>
        GetProfileResourceAsync<BzCharacterProfile>(region, realmSlug, name, "", _options.Locale, ct);

    public Task<BzEquipment?> GetEquipmentAsync(string region, string realmSlug, string name, CancellationToken ct) =>
        GetProfileResourceAsync<BzEquipment>(region, realmSlug, name, "/equipment", _options.Locale, ct);

    public Task<BzCharacterMedia?> GetMediaAsync(string region, string realmSlug, string name, CancellationToken ct) =>
        GetProfileResourceAsync<BzCharacterMedia>(region, realmSlug, name, "/character-media", _options.Locale, ct);

    /// <summary>Toujours en en_US : les noms d'instances/boss sont rapprochés de ceux de Raider.io.</summary>
    public Task<BzRaidEncounters?> GetRaidEncountersAsync(string region, string realmSlug, string name, CancellationToken ct) =>
        GetProfileResourceAsync<BzRaidEncounters>(region, realmSlug, name, "/encounters/raids", "en_US", ct);

    public Task<BzRealmIndex?> GetRealmsAsync(string region, CancellationToken ct) =>
        GetAsync<BzRealmIndex>(region, $"/data/wow/realm/index?namespace=dynamic-{region}&locale={_options.Locale}", ct);

    private Task<T?> GetProfileResourceAsync<T>(string region, string realmSlug, string name, string resource, string locale, CancellationToken ct)
        where T : class =>
        GetAsync<T>(
            region,
            $"/profile/wow/character/{Uri.EscapeDataString(realmSlug)}/{Uri.EscapeDataString(name.ToLowerInvariant())}{resource}?namespace=profile-{region}&locale={locale}",
            ct);

    private async Task<T?> GetAsync<T>(string region, string pathAndQuery, CancellationToken ct) where T : class
    {
        if (!IsConfigured)
        {
            return null;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://{region}.api.blizzard.com{pathAndQuery}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetTokenAsync(ct));

        using var response = await http.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Blizzard API {Status} sur {Path}", (int)response.StatusCode, pathAndQuery);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                _token = null;
            }

            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        return await JsonSerializer.DeserializeAsync<T>(stream, RaiderIo.RaiderIoClient.JsonOptions, ct);
    }

    private async Task<string> GetTokenAsync(CancellationToken ct)
    {
        if (_token is not null && DateTimeOffset.UtcNow < _tokenExpiresAt)
        {
            return _token;
        }

        await TokenLock.WaitAsync(ct);
        try
        {
            if (_token is not null && DateTimeOffset.UtcNow < _tokenExpiresAt)
            {
                return _token;
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://oauth.battle.net/token")
            {
                Content = new FormUrlEncodedContent([new("grant_type", "client_credentials")])
            };
            var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.ClientId}:{_options.ClientSecret}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);

            using var response = await http.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            var token = await JsonSerializer.DeserializeAsync<BzToken>(stream, RaiderIo.RaiderIoClient.JsonOptions, ct)
                        ?? throw new InvalidOperationException("Jeton Battle.net vide.");

            _token = token.AccessToken;
            _tokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn - 300);
            return _token;
        }
        finally
        {
            TokenLock.Release();
        }
    }
}

public sealed class BzToken
{
    public string AccessToken { get; set; } = "";
    public int ExpiresIn { get; set; }
}

public sealed class BzRef
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Slug { get; set; }
}

public sealed class BzTyped
{
    public string? Type { get; set; }
    public string? Name { get; set; }
}

public sealed class BzCharacterProfile
{
    public string Name { get; set; } = "";
    public int Level { get; set; }
    public BzTyped? Faction { get; set; }
    public BzRef? Race { get; set; }
    public BzRef? CharacterClass { get; set; }
    public BzRef? ActiveSpec { get; set; }
    public BzRef? Realm { get; set; }
    public BzRef? Guild { get; set; }
    public int AverageItemLevel { get; set; }
    public int EquippedItemLevel { get; set; }
}

public sealed class BzEquipment
{
    public List<BzEquippedItem> EquippedItems { get; set; } = [];
}

public sealed class BzEquippedItem
{
    public BzTyped Slot { get; set; } = new();
    public BzRef Item { get; set; } = new();
    public string Name { get; set; } = "";
    public BzTyped? Quality { get; set; }
    public BzValue? Level { get; set; }
    public List<BzEnchantment> Enchantments { get; set; } = [];
    public List<object> Sockets { get; set; } = [];
    public List<int> BonusList { get; set; } = [];
}

public sealed class BzValue
{
    public int Value { get; set; }
}

public sealed class BzEnchantment
{
    public string? DisplayString { get; set; }
}

public sealed class BzCharacterMedia
{
    public List<BzAsset> Assets { get; set; } = [];
}

public sealed class BzAsset
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}

public sealed class BzRaidEncounters
{
    public List<BzExpansionRaids> Expansions { get; set; } = [];
}

public sealed class BzExpansionRaids
{
    public BzRef Expansion { get; set; } = new();
    public List<BzInstanceRaids> Instances { get; set; } = [];
}

public sealed class BzInstanceRaids
{
    public BzRef Instance { get; set; } = new();
    public List<BzRaidMode> Modes { get; set; } = [];
}

public sealed class BzRaidMode
{
    public BzTyped Difficulty { get; set; } = new();
    public BzRaidProgress Progress { get; set; } = new();
}

public sealed class BzRaidProgress
{
    public int CompletedCount { get; set; }
    public int TotalCount { get; set; }
    public List<BzEncounterProgress> Encounters { get; set; } = [];
}

public sealed class BzEncounterProgress
{
    public BzRef Encounter { get; set; } = new();
    public int CompletedCount { get; set; }
    public long LastKillTimestamp { get; set; }
}

public sealed class BzRealmIndex
{
    public List<BzRef> Realms { get; set; } = [];
}
