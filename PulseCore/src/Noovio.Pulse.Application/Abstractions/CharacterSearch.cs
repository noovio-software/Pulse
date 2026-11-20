using Noovio.Pulse.Domain.Entities;

namespace Noovio.Pulse.Application.Abstractions;

/// <summary>
/// Recherche d'un personnage quelconque par région, royaume et nom (API Blizzard, jeton de l'application) :
/// l'analyse sans connexion Battle.net ni addon, comme la recherche d'AltPulse.
/// </summary>
public interface ICharacterSearchSource
{
    /// <summary>Royaumes de la région, triés par nom (vide si Blizzard ne répond pas).</summary>
    Task<IReadOnlyList<RealmInfo>> GetRealmsAsync(string region, CancellationToken ct);

    /// <summary>
    /// Le personnage tel que Blizzard le décrit, sous la forme d'un personnage de compte non enregistré (<c>Id</c> à 0) ;
    /// null s'il est introuvable (faute de frappe, renommé, transféré, jamais reconnecté depuis la dernière mise à jour).
    /// </summary>
    Task<UserCharacter?> FindAsync(string region, string realmSlug, string name, CancellationToken ct);
}

public sealed record RealmInfo(string Slug, string Name);

/// <summary>Régions Blizzard proposées à la recherche ; eu en tête, celle du public des sites.</summary>
public static class WowRegions
{
    public static readonly IReadOnlyList<string> All = ["eu", "us", "kr", "tw"];

    /// <summary>La région en minuscules si elle est connue, sinon eu.</summary>
    public static string Normalize(string? region) =>
        region?.Trim().ToLowerInvariant() is { } r && All.Contains(r) ? r : "eu";
}
