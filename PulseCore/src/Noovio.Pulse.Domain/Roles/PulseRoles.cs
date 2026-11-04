namespace Noovio.Pulse.Domain.Roles;

/// <summary>Les rôles connus du moteur. Mots français (locale fr_FR de l'ingestion) et anglais (au cas où la locale serait changée).</summary>
public static class PulseRoles
{
    public static readonly PulseRole Tank = new(
        "tank",
        WowSpecs.Tanks,
        StatBasis.Melee,
        ["tank"],
        [
            "absorbe", "dégâts subis", "réduit les dégâts", "réduisant les dégâts", "soigne", "points de vie maximum",
            "armure", "endurance", "esquive", "parade", "évitement", "ponction", "bouclier",
            "absorb", "damage taken", "reduces damage", "reducing damage", "heal", "armor", "stamina",
            "dodge", "parry", "avoidance", "leech", "shield", "maximum health"
        ],
        // Un coup retenu : dégâts physiques sur la cible, ou annoncés comme lourds. Les dégâts de groupe ne visent pas le tank.
        new BossHitRule(
            ["tous les personnages-joueurs", "tous les joueurs", "répartis", "all players"],
            [
                ["dégâts physiques", "dégâts de mêlée", "physical damage", "melee damage"],
                ["cible actuelle", "sa cible", "current target", "their target", "its target"]
            ],
            ["importants", "lourds", "croissants", "vulnérabilité", "subis", "heavy", "massive", "increasing", "vulnerab"],
            Threshold: 2),
        new RoleWording(
            Noun: "tank",
            NounPlural: "tanks",
            Verb: "tanker",
            SpecCount: "six",
            Classes: "guerrier, paladin, chevalier de la mort, druide, moine, chasseur de démons",
            ItemLevelImpact: "ta survie et tes dégâts",
            RoleTrinkets: "Défensifs",
            RoleTrinket: "défensif",
            DamageTrinkets: "Offensifs",
            DamageTrinket: "offensif",
            NoRoleTrinket: "Aucun bijou défensif dans le top : les deux emplacements servent aux dégâts.",
            RoleTrinketAdvice: "à activer juste avant les gros coups{0}, plutôt qu'à chaque recharge.",
            DamageTrinketAdvice: "à activer dès qu'il est prêt{0}, avec tes temps de recharge offensifs, sur un boss ou un gros pack.",
            HitsTitle: "Coups de boss à anticiper",
            HitsWithTrinket: "Active ton bijou défensif juste avant ces coups.",
            HitsWithoutTrinket: "Aucun de tes bijoux n'est défensif : aucun boss n'est à privilégier pour eux. Ces coups servent à placer tes propres temps de recharge défensifs.",
            ServerError: "Le tank est tombé. Réessaie dans un instant."));

    public static readonly PulseRole Healer = new(
        "healer",
        WowSpecs.Healers,
        StatBasis.Spell,
        ["soigneur", "healer"],
        [
            " soin", "soigne", "mana", "absorb", "bouclier", "barrière", "allié", "dégâts subis",
            " heal", "shield", "barrier", "ally", "allies", "damage taken"
        ],
        // La section du rôle ne parle déjà que de lui : un seul signal suffit, les dégâts annoncés comme lourds passent devant.
        new BossHitRule(
            [],
            [
                ["tous les personnages-joueurs", "tous les joueurs", "ensemble du groupe", "tout le groupe", "dégâts de groupe", "répartis", "all players", "group damage", "raid damage"],
                ["dégâts importants", "dégâts massifs", "heavy damage", "massive damage"],
                ["dissip", "purge", "maladie", "malédiction", "dispel", "disease", "curse", "poison"],
                ["absorbe les soins", "soins reçus", "absorption de soins", "healing absorb", "absorbs healing", "healing received"]
            ],
            ["importants", "lourds", "croissants", "continus", "heavy", "massive", "increasing", "constant"],
            Threshold: 1),
        new RoleWording(
            Noun: "soigneur",
            NounPlural: "soigneurs",
            Verb: "soigner",
            SpecCount: "sept",
            Classes: "chaman, druide, évocateur, moine, paladin, prêtre",
            ItemLevelImpact: "tes soins et ta survie",
            RoleTrinkets: "Soins",
            RoleTrinket: "de soin",
            DamageTrinkets: "Dégâts et caractéristiques",
            DamageTrinket: "de dégâts",
            NoRoleTrinket: "Aucun bijou de soin dans le top : les deux emplacements servent aux caractéristiques et aux dégâts.",
            RoleTrinketAdvice: "à activer juste avant les gros dégâts de groupe{0}, plutôt qu'à chaque recharge.",
            DamageTrinketAdvice: "à activer dès qu'il est prêt{0}, avec tes temps de recharge, sur un boss ou un gros pack.",
            HitsTitle: "Moments de boss à anticiper",
            HitsWithTrinket: "Active ton bijou de soin juste avant ces moments.",
            HitsWithoutTrinket: "Aucun de tes bijoux n'est un bijou de soin à activer : ces moments servent à placer tes propres temps de recharge.",
            ServerError: "Le soigneur est à court de mana. Réessaie dans un instant."));
}
