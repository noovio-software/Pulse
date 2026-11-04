using Noovio.FrontPulse.Domain.Enums;

namespace Noovio.FrontPulse.Domain.Entities;

/// <summary>
/// Un tank du top 100 de sa spécialisation, tel que résolu lors d'une ingestion :
/// classement Raider.io + équipement, talents et caractéristiques Blizzard.
/// Le jeu de profils d'une (saison, spécialisation) est remplacé à chaque ingestion.
/// </summary>
public sealed class TankProfile
{
    public long Id { get; set; }
    public string Season { get; set; } = "";
    public TankSpec Spec { get; set; }

    /// <summary>Rang (1 = meilleur) dans le top de la spécialisation.</summary>
    public int Rank { get; set; }

    public long RaiderIoCharacterId { get; set; }
    public string Region { get; set; } = "";
    public string RealmSlug { get; set; } = "";
    public string Name { get; set; } = "";
    public string? ProfileUrl { get; set; }

    /// <summary>Score Raider.io de la meilleure clé où le tank a été vu.</summary>
    public double BestRunScore { get; set; }
    public int BestKeyLevel { get; set; }

    /// <summary>Chaîne d'export des talents (celle de la clé Raider.io, sinon le build actif Blizzard).</summary>
    public string? LoadoutCode { get; set; }
    public string? HeroTree { get; set; }

    public double ItemLevel { get; set; }
    public List<GearPiece> Gear { get; set; } = [];
    public StatSheet? Stats { get; set; }
    public List<TalentPick> Talents { get; set; } = [];

    public DateTimeOffset FetchedAtUtc { get; set; }
}

public sealed record GearPiece(
    GearSlot Slot,
    int ItemId,
    string Name,
    int ItemLevel,
    IReadOnlyList<int> BonusIds,
    IReadOnlyList<string> Enchantments,
    IReadOnlyList<ItemEffect> Effects,
    bool IsEmbellished,
    IReadOnlyList<GemSocket>? Sockets = null,
    IReadOnlyList<ItemStat>? Stats = null);

/// <summary>Une châsse de la pièce. <see cref="GemId"/> est null quand elle est vide ; <see cref="Display"/> : texte de la gemme (« +70 Hâte »).</summary>
public sealed record GemSocket(string? SocketType, int? GemId, string? GemName, string? Display);

/// <summary>Indice secondaire porté par la pièce elle-même, hors gemmes et enchantements.</summary>
public sealed record ItemStat(SecondaryStat Stat, int Value);

/// <summary>Effet porté par un objet (on-use / proc de bijou, embellissement…).</summary>
public sealed record ItemEffect(int SpellId, string Name, string? Description);

/// <summary>Valeurs de la feuille de personnage : indice brut et pourcentage affiché en jeu.</summary>
public sealed record StatValue(double Rating, double Percent);

public sealed record StatSheet(StatValue Haste, StatValue Versatility, StatValue Mastery, StatValue Crit)
{
    public StatValue Get(SecondaryStat stat) => stat switch
    {
        SecondaryStat.Haste => Haste,
        SecondaryStat.Versatility => Versatility,
        SecondaryStat.Mastery => Mastery,
        _ => Crit
    };

    public double TotalRating => Haste.Rating + Versatility.Rating + Mastery.Rating + Crit.Rating;
}

public sealed record TalentPick(TalentTree Tree, int TalentId, int SpellId, string Name, int Rank);
