using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Noovio.Pulse.Infrastructure.RaiderIo;

namespace Noovio.Pulse.Infrastructure.Blizzard;

/// <summary>
/// Client des Profile / Game Data APIs Blizzard (https://develop.battle.net).
/// Le débit est borné par <see cref="Http.BlizzardRateLimiter"/>, branché sur le HttpClient.
/// </summary>
public sealed class BlizzardClient(HttpClient http, BlizzardOAuthClient oauth, IOptions<BlizzardOptions> options, ILogger<BlizzardClient> logger)
{
    private readonly BlizzardOptions _options = options.Value;

    public bool IsConfigured => _options.IsConfigured;

    public Task<BzEquipment?> GetEquipmentAsync(string region, string realmSlug, string name, CancellationToken ct) =>
        GetProfileResourceAsync<BzEquipment>(region, realmSlug, name, "/equipment", ct);

    public Task<BzSpecializations?> GetSpecializationsAsync(string region, string realmSlug, string name, CancellationToken ct) =>
        GetProfileResourceAsync<BzSpecializations>(region, realmSlug, name, "/specializations", ct);

    public Task<BzStatistics?> GetStatisticsAsync(string region, string realmSlug, string name, CancellationToken ct) =>
        GetProfileResourceAsync<BzStatistics>(region, realmSlug, name, "/statistics", ct);

    public Task<BzMedia?> GetItemMediaAsync(int itemId, CancellationToken ct) => GetStaticAsync<BzMedia>($"/data/wow/media/item/{itemId}", ct);

    public Task<BzMedia?> GetSpellMediaAsync(int spellId, CancellationToken ct) => GetStaticAsync<BzMedia>($"/data/wow/media/spell/{spellId}", ct);

    public Task<BzTalentTreeIndex?> GetTalentTreeIndexAsync(CancellationToken ct) => GetStaticAsync<BzTalentTreeIndex>("/data/wow/talent-tree/index", ct);

    public Task<BzTalentTree?> GetTalentTreeAsync(int treeId, int specId, CancellationToken ct) =>
        GetStaticAsync<BzTalentTree>($"/data/wow/talent-tree/{treeId}/playable-specialization/{specId}", ct);

    /// <summary>En anglais : Raider.io nomme les donjons et raids en anglais, c'est la seule façon de les retrouver dans l'index.</summary>
    public Task<BzJournalInstanceIndex?> GetJournalInstanceIndexAsync(CancellationToken ct) =>
        GetAsync<BzJournalInstanceIndex>(_options.StaticRegion, $"/data/wow/journal-instance/index?namespace=static-{_options.StaticRegion}&locale=en_US", ct);

    public Task<BzJournalInstance?> GetJournalInstanceAsync(int id, CancellationToken ct) => GetStaticAsync<BzJournalInstance>($"/data/wow/journal-instance/{id}", ct);

    public Task<BzJournalEncounter?> GetJournalEncounterAsync(int id, CancellationToken ct) => GetStaticAsync<BzJournalEncounter>($"/data/wow/journal-encounter/{id}", ct);

    public Task<BzCharacterMedia?> GetCharacterMediaAsync(string region, string realmSlug, string name, CancellationToken ct) =>
        GetProfileResourceAsync<BzCharacterMedia>(region, realmSlug, name, "/character-media", ct);

    /// <summary>
    /// Personnages du compte connecté, avec le jeton de l'utilisateur (scope wow.profile) : null si le compte
    /// n'a pas de licence WoW dans cette région.
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">Jeton utilisateur expiré ou révoqué.</exception>
    public Task<BzAccountProfile?> GetAccountProfileAsync(string region, string userAccessToken, CancellationToken ct) =>
        GetAsync<BzAccountProfile>(region, $"/profile/user/wow?namespace=profile-{region}&locale={_options.Locale}", ct, userAccessToken);

    private Task<T?> GetStaticAsync<T>(string path, CancellationToken ct) where T : class =>
        GetAsync<T>(_options.StaticRegion, $"{path}?namespace=static-{_options.StaticRegion}&locale={_options.Locale}", ct);

    private Task<T?> GetProfileResourceAsync<T>(string region, string realmSlug, string name, string resource, CancellationToken ct)
        where T : class =>
        GetAsync<T>(
            region,
            $"/profile/wow/character/{Uri.EscapeDataString(realmSlug)}/{Uri.EscapeDataString(name.ToLowerInvariant())}{resource}?namespace=profile-{region}&locale={_options.Locale}",
            ct);

    /// <summary>
    /// null si la ressource n'existe pas ou si l'API répond en erreur (le personnage est alors ignoré).
    /// Sans <paramref name="userAccessToken"/>, le jeton client_credentials de l'application est utilisé.
    /// </summary>
    private async Task<T?> GetAsync<T>(string region, string pathAndQuery, CancellationToken ct, string? userAccessToken = null) where T : class
    {
        if (!IsConfigured)
        {
            return null;
        }

        for (var attempt = 0; ; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://{region}.api.blizzard.com{pathAndQuery}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userAccessToken ?? await oauth.GetTokenAsync(ct));

            using var response = await http.SendAsync(request, ct);
            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden && userAccessToken is not null)
            {
                // Compte sans licence dans cette région.
                return null;
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized && userAccessToken is not null)
            {
                throw new UnauthorizedAccessException("Jeton Battle.net de l'utilisateur expiré ou révoqué.");
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized && attempt == 0)
            {
                await oauth.InvalidateAsync(ct);
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Blizzard API {Status} sur {Path}", (int)response.StatusCode, pathAndQuery);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            return await JsonSerializer.DeserializeAsync<T>(stream, RaiderIoClient.JsonOptions, ct);
        }
    }
}
