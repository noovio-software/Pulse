using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Noovio.Pulse.Application.Abstractions;
using Noovio.Pulse.Domain.Entities;

namespace Noovio.Pulse.Infrastructure.KeystoneGuru;

public sealed class KeystoneGuruOptions
{
    public const string SectionName = "KeystoneGuru";

    public string Email { get; set; } = "";
    public string Password { get; set; } = "";

    /// <summary>La spécification de l'API n'annonce aucun quota : un rythme modeste, pour une synchronisation qui n'est pas pressée.</summary>
    public int RequestsPerSecond { get; set; } = 2;

    public bool IsConfigured => Email.Length > 0 && Password.Length > 0;
}

/// <summary>
/// Client de l'API Keystone.guru (https://keystone.guru/api/documentation), en HTTP Basic.
/// Les endpoints documentés des routes populaires répondent 404 à ce jour : dans ce cas, les clés sont lues sur la page
/// publique du donjon, et le détail de chaque route reste lu par l'API. Le client repasse seul à l'API dès qu'elle répond.
/// </summary>
public sealed partial class KeystoneGuruClient(HttpClient http, IOptions<KeystoneGuruOptions> options, ILogger<KeystoneGuruClient> logger)
    : IDungeonRouteSource
{
    private const string GameVersion = "retail";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly AuthenticationHeaderValue _credentials = new(
        "Basic",
        Convert.ToBase64String(Encoding.UTF8.GetBytes($"{options.Value.Email}:{options.Value.Password}")));

    public async Task<IReadOnlyDictionary<int, string>> GetDungeonSlugsAsync(CancellationToken ct)
    {
        var dungeons = await GetApiAsync<KgEnvelope<List<KgDungeon>>>("api/v1/dungeon", ct);
        return (dungeons?.Data ?? [])
            .Where(d => d.ChallengeModeId is not null)
            .GroupBy(d => d.ChallengeModeId!.Value)
            .ToDictionary(g => g.Key, g => g.Last().Slug);
    }

    public async Task<IReadOnlyList<string>> GetPopularRouteKeysAsync(string dungeonSlug, int count, CancellationToken ct)
    {
        var slug = Uri.EscapeDataString(dungeonSlug);
        var popular = await GetApiAsync<KgEnvelope<List<KgRouteKey>>>($"api/v1/route/{GameVersion}/{slug}/popular?count={count}", ct);
        if (popular is not null)
        {
            return popular.Data.Select(r => r.PublicKey).ToList();
        }

        logger.LogInformation("Routes populaires de {Dungeon} absentes de l'API : clés lues sur la page publique", dungeonSlug);
        using var response = await http.GetAsync($"routes/{GameVersion}/{slug}", ct);
        response.EnsureSuccessStatusCode();
        return ParseRouteKeys(await response.Content.ReadAsStringAsync(ct), dungeonSlug).Take(count).ToList();
    }

    public async Task<DungeonRoute?> GetRouteAsync(string publicKey, CancellationToken ct)
    {
        var route = await GetApiAsync<KgEnvelope<KgRoute>>($"api/v1/route/{Uri.EscapeDataString(publicKey)}", ct);
        return route?.Data is { } data ? ToRoute(data) : null;
    }

    /// <summary>Clés des liens vers les routes de ce donjon, dans l'ordre de la page, sans doublon.</summary>
    public static IReadOnlyList<string> ParseRouteKeys(string html, string dungeonSlug) =>
        RouteLink().Matches(html)
            .Where(m => m.Groups["dungeon"].Value == dungeonSlug)
            .Select(m => m.Groups["key"].Value)
            .Distinct()
            .ToList();

    /// <summary>Convertit la réponse de <c>GET /api/v1/route/{key}</c>.</summary>
    public static DungeonRoute? ParseRoute(string json) =>
        JsonSerializer.Deserialize<KgEnvelope<KgRoute>>(json, JsonOptions)?.Data is { } data ? ToRoute(data) : null;

    private async Task<T?> GetApiAsync<T>(string path, CancellationToken ct) where T : class
    {
        // Les identifiants ne partent que vers l'API, pas vers les pages publiques lues avec le même client.
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = _credentials;
        request.Headers.Accept.ParseAdd("application/json");

        using var response = await http.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        await using var body = await response.Content.ReadAsStreamAsync(ct);
        return await JsonSerializer.DeserializeAsync<T>(body, JsonOptions, ct);
    }

    private static DungeonRoute ToRoute(KgRoute r) => new(
        r.PublicKey,
        r.Title,
        r.Author?.Name,
        r.Author?.Links?.View,
        r.EnemyForces ?? 0,
        r.EnemyForcesRequired ?? 0,
        (r.Pulls ?? []).Select(p => p.Enemies?.Count ?? 0).ToList(),
        r.Links?.View ?? "",
        r.Links?.Embed ?? "",
        r.Links?.Thumbnails?.FirstOrDefault(),
        ReadSeason(r.AffixGroups),
        DateTimeOffset.TryParse(r.ExpiresAt, out var expires) ? expires : null);

    /// <summary>La spécification annonce un tableau de groupes d'affixes, l'API renvoie un objet : les deux sont acceptés.</summary>
    private static int? ReadSeason(JsonElement? affixGroups)
    {
        if (affixGroups is not { } groups)
        {
            return null;
        }

        var candidates = groups.ValueKind == JsonValueKind.Array ? groups.EnumerateArray().ToList() : [groups];
        return candidates
            .Where(g => g.ValueKind == JsonValueKind.Object && g.TryGetProperty("season", out var s) && s.ValueKind == JsonValueKind.Number)
            .Select(g => (int?)g.GetProperty("season").GetInt32())
            .Max();
    }

    [GeneratedRegex("""/route/(?<dungeon>[a-z0-9-]+)/(?<key>[A-Za-z0-9]+)/[a-z0-9-]*["']""")]
    private static partial Regex RouteLink();

    private sealed record KgEnvelope<T>(T Data);

    private sealed record KgDungeon(string Slug, int? ChallengeModeId);

    private sealed record KgRouteKey(string PublicKey);

    private sealed record KgRoute(
        string PublicKey,
        string Title,
        int? EnemyForces,
        int? EnemyForcesRequired,
        string? ExpiresAt,
        KgUser? Author,
        JsonElement? AffixGroups,
        KgLinks? Links,
        List<KgPull>? Pulls);

    private sealed record KgUser(string? Name, KgUserLinks? Links);

    private sealed record KgUserLinks(string? View);

    private sealed record KgLinks(string? View, string? Embed, List<string>? Thumbnails);

    private sealed record KgPull(List<KgEnemy>? Enemies);

    private sealed record KgEnemy(int NpcId);
}
