using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.JSInterop;
using AltPulse.Client.Models;

namespace AltPulse.Client.Services;

public sealed record HistoryEntry(
    string Region,
    string RealmSlug,
    string RealmName,
    string Name,
    int ClassId,
    DateTimeOffset LastSearchedUtc)
{
    [JsonIgnore]
    public string Key => CharacterKey.Of(Region, RealmSlug, Name);
    [JsonIgnore]
    public string Label => $"{Name} - {RealmName} ({Region.ToUpperInvariant()})";
}

public sealed record AltPulseSettings
{
    public string SortBy { get; init; } = "recent";
    public string LastRegion { get; init; } = "eu";
    public HashSet<string> CollapsedSections { get; init; } = [];

    /// <summary>Colonne des libellés réduite aux icônes. null = jamais choisi : réduite par défaut sur petit écran.</summary>
    public bool? LabelsCompact { get; init; }
}

/// <summary>
/// Historique des personnages recherchés + préférences d'affichage,
/// persistés dans le localStorage du navigateur (jamais côté serveur).
/// Ne doit être appelé qu'après le premier rendu interactif (pas de JS pendant le prérendu).
/// </summary>
public sealed class LocalHistoryStore(IJSRuntime js)
{
    private const string HistoryKey = "altpulse.history";
    private const string SettingsKey = "altpulse.settings";
    private const int MaxEntries = 30;

    /// <summary>Clés utilisées avant le renommage en AltPulse, migrées à la première lecture.</summary>
    private static readonly Dictionary<string, string> LegacyKeys = new()
    {
        [HistoryKey] = "noovio.history",
        [SettingsKey] = "noovio.settings"
    };

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<List<HistoryEntry>> GetHistoryAsync() =>
        await ReadAsync<List<HistoryEntry>>(HistoryKey) ?? [];

    public async Task<List<HistoryEntry>> AddOrTouchAsync(CharacterSnapshot character)
    {
        var history = await GetHistoryAsync();
        history.RemoveAll(h => h.Key == character.Key);
        history.Insert(0, new HistoryEntry(
            character.Region,
            character.RealmSlug,
            character.RealmName,
            character.Name,
            character.ClassId,
            DateTimeOffset.UtcNow));

        if (history.Count > MaxEntries)
        {
            history.RemoveRange(MaxEntries, history.Count - MaxEntries);
        }

        await WriteAsync(HistoryKey, history);
        return history;
    }

    public async Task<List<HistoryEntry>> RemoveAsync(string key)
    {
        var history = await GetHistoryAsync();
        history.RemoveAll(h => h.Key == key);
        await WriteAsync(HistoryKey, history);
        return history;
    }

    public async Task ClearAsync() => await js.InvokeVoidAsync("localStorage.removeItem", HistoryKey);

    public async Task<AltPulseSettings> GetSettingsAsync() =>
        await ReadAsync<AltPulseSettings>(SettingsKey) ?? new AltPulseSettings();

    public async Task SaveSettingsAsync(AltPulseSettings settings) => await WriteAsync(SettingsKey, settings);

    private async Task<T?> ReadAsync<T>(string key)
    {
        try
        {
            var json = await js.InvokeAsync<string?>("localStorage.getItem", key);
            if (string.IsNullOrWhiteSpace(json) && LegacyKeys.TryGetValue(key, out var legacyKey))
            {
                json = await js.InvokeAsync<string?>("localStorage.getItem", legacyKey);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    await js.InvokeVoidAsync("localStorage.setItem", key, json);
                    await js.InvokeVoidAsync("localStorage.removeItem", legacyKey);
                }
            }

            return string.IsNullOrWhiteSpace(json) ? default : JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (JsonException)
        {
            // Données corrompues ou d'un ancien format : on repart de zéro.
            return default;
        }
    }

    private async Task WriteAsync<T>(string key, T value) =>
        await js.InvokeVoidAsync("localStorage.setItem", key, JsonSerializer.Serialize(value, JsonOptions));
}
