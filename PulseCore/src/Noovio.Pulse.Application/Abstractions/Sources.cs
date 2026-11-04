using Noovio.Pulse.Application.Ingestion;
using Noovio.Pulse.Domain.Entities;
using Noovio.Pulse.Domain.Enums;

namespace Noovio.Pulse.Application.Abstractions;

/// <summary>Phase 1 : classement des joueurs du rôle (Raider.io).</summary>
public interface ILeaderboardSource
{
    Task<string> GetCurrentSeasonAsync(CancellationToken ct);

    /// <summary>
    /// Joueurs du rôle par spécialisation, dans l'ordre du classement des clés, dédoublonnés,
    /// jusqu'à <paramref name="perSpec"/> par spécialisation.
    /// </summary>
    /// <param name="progress">Avancement, rendu après chaque requête (région, donjon) parcourue.</param>
    Task<IReadOnlyDictionary<WowSpec, IReadOnlyList<LeaderboardPlayer>>> GetTopPlayersAsync(string season, int perSpec, CancellationToken ct,
        IProgress<LeaderboardStep>? progress = null);
}

/// <summary>Phase 2 : équipement, talents et caractéristiques d'un personnage (API Blizzard).</summary>
public interface ICharacterLoadoutSource
{
    /// <summary>null si le personnage est introuvable (renommé, transféré, profil privé).</summary>
    Task<CharacterLoadout?> GetLoadoutAsync(LeaderboardPlayer player, CancellationToken ct);
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
    Task<TalentTreeLayout?> GetLayoutAsync(WowSpec spec, CancellationToken ct);
}

/// <summary>Donjons de la saison et raids de l'extension : rencontres, butin et techniques (Journal d'aventure).</summary>
public interface IEncounterJournalSource
{
    /// <summary>null si le journal n'a pas pu être lu : l'analyse se fait alors sans les sources de butin.</summary>
    Task<EncounterJournal?> GetAsync(string season, CancellationToken ct);
}

/// <summary>Écriture des résultats d'une ingestion.</summary>
public interface IMetaStore
{
    /// <summary>Remplace, en une transaction, les profils et le snapshot d'une (saison, spécialisation).</summary>
    Task ReplaceAsync(string season, WowSpec spec, IReadOnlyList<PlayerProfile> profiles, SpecMetaSnapshot snapshot, CancellationToken ct);

    /// <summary>Profils déjà ingérés, par rang : pour recalculer un snapshot sans relire les API.</summary>
    Task<IReadOnlyList<PlayerProfile>> GetProfilesAsync(string season, WowSpec spec, CancellationToken ct);

    /// <summary>Remplace le snapshot seul, les profils restant tels quels.</summary>
    Task ReplaceSnapshotAsync(SpecMetaSnapshot snapshot, CancellationToken ct);
}

/// <summary>Lecture côté site : uniquement des snapshots pré-calculés.</summary>
public interface ISpecMetaQueries
{
    Task<SpecMetaSnapshot?> GetLatestAsync(WowSpec spec, CancellationToken ct = default);

    Task<IReadOnlyList<SpecMetaSummary>> GetSummariesAsync(CancellationToken ct = default);
}

public sealed record LeaderboardPlayer(
    long RaiderIoCharacterId,
    WowSpec Spec,
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
    WowSpec Spec,
    string Season,
    DateTimeOffset ComputedAtUtc,
    int SampleSize,
    double MedianItemLevel,
    int MedianKeyLevel,
    string? TopHeroTree,
    string? TopHeroIconUrl);

public sealed record EncounterJournal(IReadOnlyList<JournalInstance> Instances);

public enum InstanceKind
{
    Dungeon,
    Raid
}

/// <summary><see cref="RaiderIoName"/> : nom anglais, celui des clés Raider.io ; <see cref="Name"/> : nom affiché.</summary>
public sealed record JournalInstance(int Id, string Name, string RaiderIoName, InstanceKind Kind, IReadOnlyList<JournalBoss> Bosses);

public sealed record JournalBoss(int Id, string Name, IReadOnlyList<int> ItemIds, IReadOnlyList<JournalAbility> Abilities);

/// <summary>Une technique. <see cref="RoleHeading"/> : rangée sous un titre de section mentionnant le rôle.</summary>
public sealed record JournalAbility(string Name, string Text, bool RoleHeading);
