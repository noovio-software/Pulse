using AltPulse.Client.Models;

namespace AltPulse.Client.Services;

/// <summary>
/// Contrat commun : implémenté côté serveur (appels directs Blizzard + Raider.io)
/// et côté WebAssembly (appels HTTP vers les endpoints /api du serveur).
/// </summary>
public interface IAltPulseApi
{
    Task<SeasonInfo?> GetSeasonAsync(string region, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RealmInfo>> GetRealmsAsync(string region, CancellationToken cancellationToken = default);

    Task<CharacterLookupResult> GetCharacterAsync(string region, string realm, string name, bool forceRefresh = false, CancellationToken cancellationToken = default);
}
