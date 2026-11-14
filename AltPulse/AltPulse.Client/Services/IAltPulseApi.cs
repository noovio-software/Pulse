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

    /// <summary>Lit la chaîne « alts » de l'addon NoovioPulse (copiée en jeu) : un personnage par alt connu du compte.</summary>
    Task<AddonImportResult> ImportAddonAsync(string text, CancellationToken cancellationToken = default);
}
