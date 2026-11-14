using System.Net;
using System.Net.Http.Json;
using AltPulse.Client.Models;

namespace AltPulse.Client.Services;

public sealed class HttpAltPulseApi(HttpClient http) : IAltPulseApi
{
    public async Task<SeasonInfo?> GetSeasonAsync(string region, CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync($"api/season/{region}", cancellationToken);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<SeasonInfo>(cancellationToken)
            : null;
    }

    public async Task<IReadOnlyList<RealmInfo>> GetRealmsAsync(string region, CancellationToken cancellationToken = default) =>
        await http.GetFromJsonAsync<List<RealmInfo>>($"api/realms/{region}", cancellationToken) ?? [];

    public async Task<CharacterLookupResult> GetCharacterAsync(string region, string realm, string name, bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        var url = $"api/characters/{region}/{Uri.EscapeDataString(realm)}/{Uri.EscapeDataString(name)}?refresh={forceRefresh}";
        using var response = await http.GetAsync(url, cancellationToken);

        if (response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound)
        {
            return await response.Content.ReadFromJsonAsync<CharacterLookupResult>(cancellationToken)
                   ?? new CharacterLookupResult(null, "Réponse vide du serveur.");
        }

        return new CharacterLookupResult(null, $"Erreur serveur ({(int)response.StatusCode}).");
    }

    public async Task<AddonImportResult> ImportAddonAsync(string text, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync("api/addon", new AddonImportRequest(text), cancellationToken);
        if (response.StatusCode is HttpStatusCode.OK or HttpStatusCode.BadRequest)
        {
            return await response.Content.ReadFromJsonAsync<AddonImportResult>(cancellationToken)
                   ?? new AddonImportResult([], "Réponse vide du serveur.");
        }

        return new AddonImportResult([], $"Erreur serveur ({(int)response.StatusCode}).");
    }
}

public sealed record AddonImportRequest(string Text);
