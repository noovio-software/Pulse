namespace Noovio.Pulse.Usage;

/// <summary>Issue d'un bench : les valeurs de <c>AnalysisStatus</c>, plus <see cref="Error"/> (Blizzard ou Raider.io injoignable).</summary>
public static class BenchStatus
{
    public const string Ok = "Ok";
    public const string NoRoleSpec = "NoRoleSpec";
    public const string NoMeta = "NoMeta";
    public const string CharacterNotFound = "CharacterNotFound";
    public const string Error = "Error";
}

/// <summary>
/// Un bench lancé sur FrontPulse, LifePulse ou BurstPulse : qui, quel personnage, et ce que l'analyse en a dit.
/// Une ligne plate, une colonne par mesure suivie, pour que PulseAdmin agrège sans relire de JSON ;
/// les colonnes de l'analyse restent nulles quand le bench n'a pas abouti.
/// </summary>
public sealed class BenchEvent
{
    public long Id { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }

    /// <summary>« FrontPulse », « LifePulse », « BurstPulse ».</summary>
    public string App { get; set; } = "";

    /// <summary>« tank », « healer », « dps » (rôle Raider.io).</summary>
    public string Role { get; set; } = "";

    /// <summary>Voir <see cref="BenchStatus"/>.</summary>
    public string Status { get; set; } = BenchStatus.Ok;

    /// <summary>Résultat servi par le cache de 10 minutes du site, sans relire Blizzard.</summary>
    public bool FromCache { get; set; }

    /// <summary>Bouton « Actualiser » : le cache a été vidé avant le bench.</summary>
    public bool Forced { get; set; }

    public int DurationMs { get; set; }

    // Qui
    public long BattleNetId { get; set; }

    // Le personnage
    public string Region { get; set; } = "";
    public string RealmSlug { get; set; } = "";
    public string RealmName { get; set; } = "";
    public string CharacterName { get; set; } = "";
    public int ClassId { get; set; }
    public string ClassName { get; set; } = "";
    public int Level { get; set; }
    public string? Race { get; set; }
    public string? Faction { get; set; }

    // La spécialisation comparée
    public int? SpecId { get; set; }
    public string? SpecSlug { get; set; }
    public string? SpecName { get; set; }

    /// <summary>Le personnage joue la spécialisation comparée (sinon il est comparé à une autre que la sienne).</summary>
    public bool? PlaysSpec { get; set; }

    // L'analyse
    public int? Score { get; set; }
    public double? ItemLevel { get; set; }

    /// <summary>Niveau d'objet médian du top de la spécialisation.</summary>
    public double? TopItemLevel { get; set; }

    /// <summary>Pourcentages de la feuille de personnage.</summary>
    public double? Haste { get; set; }
    public double? Crit { get; set; }
    public double? Mastery { get; set; }
    public double? Versatility { get; set; }

    public double? HasteRating { get; set; }
    public double? CritRating { get; set; }
    public double? MasteryRating { get; set; }
    public double? VersatilityRating { get; set; }

    /// <summary>Pourcentages médians du top, pour mesurer l'écart.</summary>
    public double? TopHaste { get; set; }
    public double? TopCrit { get; set; }
    public double? TopMastery { get; set; }
    public double? TopVersatility { get; set; }

    /// <summary>Pièces analysées et leur verdict.</summary>
    public int? GearPieces { get; set; }
    public int? GearGood { get; set; }
    public int? GearImprove { get; set; }
    public int? GearProblem { get; set; }

    /// <summary>Popularité moyenne, dans le top, des pièces portées (0 à 1).</summary>
    public double? GearTopShare { get; set; }

    public int? EnchantsExpected { get; set; }
    public int? EnchantsMissing { get; set; }
    public int? Sockets { get; set; }
    public int? SocketsEmpty { get; set; }
    public int? Embellishments { get; set; }

    /// <summary>Ressemblance des talents avec le top (0 à 1).</summary>
    public double? TalentSimilarity { get; set; }

    public string? HeroTree { get; set; }
    public string? TopHeroTree { get; set; }

    public double? MythicScore { get; set; }

    public int? FindingsGood { get; set; }
    public int? FindingsImprove { get; set; }
    public int? FindingsProblem { get; set; }

    /// <summary>Verdict par domaine analysé (« ItemLevel » → « Good »…).</summary>
    public Dictionary<string, string>? Areas { get; set; }

    /// <summary>Niveau d'objet par emplacement (« Head » → 678…).</summary>
    public Dictionary<string, int>? SlotItemLevels { get; set; }

    /// <summary>Type d'erreur quand <see cref="Status"/> vaut <see cref="BenchStatus.Error"/>.</summary>
    public string? Error { get; set; }
}
