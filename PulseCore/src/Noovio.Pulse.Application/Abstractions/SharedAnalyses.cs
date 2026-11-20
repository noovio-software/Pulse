using Noovio.Pulse.Application.Analysis;
using Noovio.Pulse.Domain.Entities;

namespace Noovio.Pulse.Application.Abstractions;

/// <summary>
/// Analyses partagées par lien : une copie figée de l'analyse affichée (méta du top comprise). Relancer l'analyse
/// ou la mise à jour de la méta ne change jamais un lien déjà donné.
/// </summary>
public interface ISharedAnalysisStore
{
    /// <summary>
    /// Jeton du lien de partage de cette analyse. Partager deux fois la même analyse (même personnage, même
    /// spécialisation, même date d'analyse) redonne le même lien ; une analyse relancée en crée un nouveau.
    /// </summary>
    Task<string> ShareAsync(long battleNetId, UserCharacter character, CharacterAnalysis analysis, CancellationToken ct);

    /// <summary>null si le jeton ne correspond à aucun partage.</summary>
    Task<SharedAnalysis?> GetAsync(string token, CancellationToken ct);
}

/// <param name="RealmName">Nom affiché du royaume (l'analyse ne porte que son slug).</param>
public sealed record SharedAnalysis(string Token, string RealmName, CharacterAnalysis Analysis, DateTimeOffset SharedAtUtc);
