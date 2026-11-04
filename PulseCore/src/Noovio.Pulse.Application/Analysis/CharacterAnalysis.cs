using Noovio.Pulse.Application.Abstractions;
using Noovio.Pulse.Domain;
using Noovio.Pulse.Domain.Entities;
using Noovio.Pulse.Domain.Enums;

namespace Noovio.Pulse.Application.Analysis;

public enum Verdict
{
    Good,
    Improve,
    Problem
}

public enum AnalysisStatus
{
    Ok,

    /// <summary>La classe du personnage n'a pas de spécialisation dans le rôle.</summary>
    NoRoleSpec,

    /// <summary>Aucun snapshot méta pour la spécialisation (ingestion pas encore passée).</summary>
    NoMeta,

    /// <summary>Profil introuvable côté Blizzard (renommé, transféré, jamais connecté depuis le dernier patch).</summary>
    CharacterNotFound
}

public sealed record AnalysisResult(AnalysisStatus Status, CharacterAnalysis? Analysis, SpecInfo? Spec = null);

/// <summary>
/// Comparaison d'un personnage avec la méta de sa spécialisation dans le rôle : un verdict par domaine,
/// les constats détaillés (quoi, pourquoi, comment) et les données des vues visuelles.
/// </summary>
public sealed record CharacterAnalysis(
    SpecInfo Spec,
    CharacterDetails Character,
    SpecMetaSnapshot Meta,
    bool PlaysSpec,
    int Score,
    IReadOnlyList<AnalysisArea> Areas,
    IReadOnlyList<AnalysisFinding> Findings,
    IReadOnlyList<GearReview> Gear,
    IReadOnlyList<StatReview> Stats,
    TalentReview Talents,
    GemEnchantReview GemsAndEnchants,
    FarmRoadmap Roadmap,
    TrinketPlan TrinketPlan,
    DateTimeOffset AnalyzedAtUtc);

/// <summary>Un domaine analysé (niveau d'objet, talents…) et son verdict d'ensemble.</summary>
public sealed record AnalysisArea(AnalysisAreaKind Kind, Verdict Verdict, string Summary);

public enum AnalysisAreaKind
{
    ItemLevel,
    Stats,
    Talents,
    Trinkets,
    Embellishments,
    Enchantments,
    Gems,
    MythicPlus
}

/// <summary>Un constat : ce qui va ou ne va pas, pourquoi, et comment l'améliorer.</summary>
public sealed record AnalysisFinding(AnalysisAreaKind Area, Verdict Verdict, string Title, string Why, string How, string? IconUrl = null);

/// <summary>
/// Une pièce portée face au top : niveau d'objet comparé à la médiane du top sur l'emplacement,
/// popularité de la pièce, enchantement attendu.
/// </summary>
public sealed record GearReview(
    GearSlot Slot,
    int ItemId,
    string Name,
    string? IconUrl,
    int ItemLevel,
    int TopItemLevel,
    double TopShare,
    ItemStatSnapshot? TopPick,
    bool EnchantExpected,
    bool HasEnchant,
    string? SuggestedEnchant,
    Verdict Verdict);

/// <summary>Une caractéristique secondaire du personnage placée sur la fourchette du top.</summary>
public sealed record StatReview(SecondaryStat Stat, double Percent, double Rating, StatDistribution Top, Verdict Verdict);

public sealed record TalentReview(
    string? LoadoutCode,
    string? HeroTree,
    string? TopHeroTree,
    IReadOnlyList<TalentPickRate> MissingCore,
    IReadOnlyList<TalentPickRate> Atypical,
    IReadOnlyList<int> SelectedTalentIds,
    double Similarity);
