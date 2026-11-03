using Noovio.FrontPulse.Application.Ingestion;
using Noovio.FrontPulse.Domain.Entities;
using Noovio.FrontPulse.Domain.Enums;

namespace Noovio.FrontPulse.Application.Abstractions;

/// <summary>Phase 1 : classement des tanks (Raider.io).</summary>
public interface ILeaderboardSource
{
    Task<string> GetCurrentSeasonAsync(CancellationToken ct);

    /// <summary>
    /// Tanks par spécialisation, dans l'ordre du classement des clés, dédoublonnés,
    /// jusqu'à <paramref name="perSpec"/> par spécialisation.
    /// </summary>
    /// <param name="progress">Avancement, rendu après chaque requête (région, donjon) parcourue.</param>
    Task<IReadOnlyDictionary<TankSpec, IReadOnlyList<LeaderboardTank>>> GetTopTanksAsync(string season, int perSpec, CancellationToken ct,
        IProgress<LeaderboardStep>? progress = null);
}

/// <summary>Phase 2 : équipement, talents et caractéristiques d'un personnage (API Blizzard).</summary>
public interface ICharacterLoadoutSource
{
    /// <summary>null si le personnage est introuvable (renommé, transféré, profil privé).</summary>
    Task<CharacterLoadout?> GetLoadoutAsync(LeaderboardTank tank, CancellationToken ct);
}

/// <summary>Icônes des objets et des sorts, pour l'affichage (API Blizzard, mise en cache longue).</summary>
public interface IMediaSource
{
    Task<IReadOnlyDictionary<int, string>> GetItemIconsAsync(IEnumerable<int> itemIds, CancellationToken ct);

    Task<IReadOnlyDictionary<int, string>> GetSpellIconsAsync(IEnumerable<int> spellIds, CancellationToken ct);
}

/// <summary>Disposition de l'arbre de talents d'une spécialisation, icônes comprises, taux de sélection à zéro.</summary>
public interface ITalentTreeSource
{
    Task<TalentTreeLayout?> GetLayoutAsync(TankSpec spec, CancellationToken ct);
}

/// <summary>Écriture des résultats d'une ingestion.</summary>
public interface ITankMetaStore
{
    /// <summary>Remplace, en une transaction, les profils et le snapshot d'une (saison, spécialisation).</summary>
    Task ReplaceAsync(string season, TankSpec spec, IReadOnlyList<TankProfile> profiles, SpecMetaSnapshot snapshot, CancellationToken ct);

    /// <summary>Profils déjà ingérés, par rang : pour recalculer un snapshot sans relire les API.</summary>
    Task<IReadOnlyList<TankProfile>> GetProfilesAsync(string season, TankSpec spec, CancellationToken ct);

    /// <summary>Remplace le snapshot seul, les profils restant tels quels.</summary>
    Task ReplaceSnapshotAsync(SpecMetaSnapshot snapshot, CancellationToken ct);
}

/// <summary>Lecture côté site : uniquement des snapshots pré-calculés.</summary>
public interface ISpecMetaQueries
{
    Task<SpecMetaSnapshot?> GetLatestAsync(TankSpec spec, CancellationToken ct = default);

    Task<IReadOnlyList<SpecMetaSummary>> GetSummariesAsync(CancellationToken ct = default);
}

public sealed record LeaderboardTank(
    long RaiderIoCharacterId,
    TankSpec Spec,
    string Name,
    string Region,
    string RealmSlug,
    string? ProfileUrl,
    double BestRunScore,
    int BestKeyLevel,
    string? LoadoutCode);

public sealed record CharacterLoadout(
    int? ActiveSpecId,
    double ItemLevel,
    IReadOnlyList<GearPiece> Gear,
    StatSheet? Stats,
    IReadOnlyList<TalentPick> Talents,
    string? LoadoutCode,
    string? HeroTree);

public sealed record SpecMetaSummary(
    TankSpec Spec,
    string Season,
    DateTimeOffset ComputedAtUtc,
    int SampleSize,
    double MedianItemLevel,
    int MedianKeyLevel,
    string? TopHeroTree,
    string? TopHeroIconUrl);
