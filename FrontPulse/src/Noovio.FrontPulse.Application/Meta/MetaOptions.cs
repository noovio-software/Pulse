namespace Noovio.FrontPulse.Application.Meta;

public sealed class MetaOptions
{
    public const string SectionName = "FrontPulse:Meta";

    public int TopItemsPerSlot { get; set; } = 5;
    public int TopTrinketsPerOrientation { get; set; } = 5;
    public int TopEmbellishmentCombos { get; set; } = 3;
    public int TopTalentBuilds { get; set; } = 3;

    /// <summary>Bijoux forcés en « défensif » / « offensif », prioritaires sur l'heuristique des mots-clés.</summary>
    public HashSet<int> DefensiveTrinketIds { get; set; } = [];
    public HashSet<int> OffensiveTrinketIds { get; set; } = [];

    /// <summary>
    /// Un bijou dont un effet contient l'un de ces mots est classé défensif. Les descriptions Blizzard
    /// ne portent pas de rôle : c'est une heuristique, corrigeable par les listes ci-dessus.
    /// Mots français (locale fr_FR de l'ingestion) et anglais (au cas où la locale serait changée).
    /// </summary>
    public string[] DefensiveKeywords { get; set; } =
    [
        "absorbe", "dégâts subis", "réduit les dégâts", "réduisant les dégâts", "soigne", "points de vie maximum",
        "armure", "endurance", "esquive", "parade", "évitement", "ponction", "bouclier",
        "absorb", "damage taken", "reduces damage", "reducing damage", "heal", "armor", "stamina",
        "dodge", "parry", "avoidance", "leech", "shield", "maximum health"
    ];
}
