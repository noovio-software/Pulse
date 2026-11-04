using Noovio.Pulse.Domain.Enums;

namespace Noovio.Pulse.Domain.Entities;

/// <summary>
/// Snapshot méta pré-calculé d'une spécialisation pour une saison : c'est la seule chose que
/// lit le site, sans aucun calcul à la volée. Une ligne par (saison, spécialisation), écrasée à chaque ingestion.
/// </summary>
public sealed class SpecMeta
{
    public long Id { get; set; }
    public string Season { get; set; } = "";
    public WowSpec Spec { get; set; }
    public DateTimeOffset ComputedAtUtc { get; set; }
    public int SampleSize { get; set; }
    public SpecMetaSnapshot Snapshot { get; set; } = default!;
}

public sealed record SpecMetaSnapshot(
    WowSpec Spec,
    string Season,
    DateTimeOffset ComputedAtUtc,
    int SampleSize,
    double MedianItemLevel,
    int MedianKeyLevel,
    IReadOnlyList<StatDistribution> Stats,
    IReadOnlyList<SlotUsage> Slots,
    IReadOnlyList<ItemStatSnapshot> RoleTrinkets,
    IReadOnlyList<ItemStatSnapshot> DamageTrinkets,
    IReadOnlyList<EmbellishmentCombo> Embellishments,
    IReadOnlyList<TalentBuild> TalentBuilds,
    IReadOnlyList<HeroTreeUsage> HeroTrees,
    IReadOnlyList<TalentPickRate> Talents,
    IReadOnlyList<TopPlayer> Players,
    TalentTreeLayout? TalentTree = null);

/// <summary>Popularité d'un objet dans l'échantillon. <see cref="Share"/> est dans [0, 1].</summary>
public sealed record ItemStatSnapshot(int ItemId, string Name, string? IconUrl, int Count, double Share, int MedianItemLevel);

public sealed record SlotUsage(GearSlot Slot, IReadOnlyList<ItemStatSnapshot> Items);

/// <summary>
/// Une caractéristique secondaire dans le top. Les pourcentages sont ceux de la feuille de personnage ;
/// <see cref="P25Percent"/>–<see cref="P75Percent"/> encadrent la moitié centrale du top.
/// <see cref="MedianShare"/> (part de l'indice dans le total des quatre secondaires) donne l'ordre de priorité.
/// </summary>
public sealed record StatDistribution(
    SecondaryStat Stat,
    double MedianRating,
    double MedianPercent,
    double MedianShare,
    double P25Percent = 0,
    double P75Percent = 0);

public sealed record EmbellishmentCombo(IReadOnlyList<EmbellishmentPart> Parts, int Count, double Share);

/// <summary>
/// Un embellissement : nommé par son effet (sort) quand la pièce en porte un, sinon par la pièce craftée.
/// <see cref="ItemId"/> est la pièce qui le porte : son icône sert quand le sort n'en a pas (cas courant).
/// <see cref="Count"/> : nombre de pièces portant ce même embellissement (×2).
/// </summary>
public sealed record EmbellishmentPart(string Name, int SpellId, int ItemId, string? IconUrl, int Count = 1);

public sealed record TalentBuild(string LoadoutCode, string? HeroTree, int Count, double Share);

public sealed record HeroTreeUsage(string Name, int Count, double Share);

public sealed record TalentPickRate(TalentTree Tree, int TalentId, int SpellId, string Name, double AverageRank, double Share);

/// <summary>
/// Arbre de talents tel que disposé en jeu (classe, héros, spécialisation), avec le taux de sélection
/// de chaque nœud dans le top. Les coordonnées sont en colonnes/rangées, relatives au coin de la section.
/// </summary>
public sealed record TalentTreeLayout(IReadOnlyList<TalentTreeSection> Sections);

/// <summary><see cref="Share"/> : part du top jouant cet arbre (héros) ; 1 pour la classe et la spécialisation.</summary>
public sealed record TalentTreeSection(
    TalentTree Tree,
    string Name,
    string? IconUrl,
    double Share,
    double Width,
    double Height,
    IReadOnlyList<TalentNodeView> Nodes);

public sealed record TalentNodeView(
    int NodeId,
    double X,
    double Y,
    TalentNodeKind Kind,
    int MaxRank,
    IReadOnlyList<int> Unlocks,
    IReadOnlyList<TalentChoiceView> Choices);

/// <summary>Une option d'un nœud (une seule, sauf pour les nœuds à choix).</summary>
public sealed record TalentChoiceView(int TalentId, int SpellId, string Name, string? IconUrl, double Share);

public sealed record TopPlayer(int Rank, string Name, string Region, string RealmSlug, double BestRunScore, int BestKeyLevel, double ItemLevel, string? ProfileUrl);
