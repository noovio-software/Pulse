namespace Noovio.Pulse.Domain.Roles;

/// <summary>Les rôles connus du moteur. Mots français (locale fr_FR de l'ingestion) et anglais (au cas où la locale serait changée).</summary>
public static class PulseRoles
{
    public static readonly PulseRole Tank = new(
        "tank",
        RoleKind.Tank,
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
        new RoleWording("Tank"));

    public static readonly PulseRole Healer = new(
        "healer",
        RoleKind.Healer,
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
        new RoleWording("Healer"));

    /// <summary>
    /// Les bijoux du rôle sont ceux qui infligent des dégâts eux-mêmes ; les autres donnent des caractéristiques.
    /// Le mêlée lit sa feuille sur les lignes de mêlée ; les chasseurs à distance et les lanceurs de sorts précisent les leurs dans <see cref="WowSpecs.Damage"/>.
    /// </summary>
    public static readonly PulseRole Damage = new(
        "dps",
        RoleKind.Damage,
        WowSpecs.Damage,
        StatBasis.Melee,
        ["dps", "damage dealer"],
        [
            "inflige", "points de dégâts", "deals", "dealing", "damage to"
        ],
        // Ce qui demande de garder son burst : des renforts, une cible à abattre avant une échéance, un bouclier à percer.
        // La section du rôle ne parle déjà que de lui : un seul signal suffit, l'urgence fait passer devant.
        new BossHitRule(
            [],
            [
                ["invoqu", "libère", "éclore", "se joignent", "renfort", "summon", "spawn", "adds"],
                [" tué", "détruit", "destruction", "défaite", "élimin", " kill", "destroy", "defeat"],
                ["bouclier", "absorbant les dégâts", "vulnérab", "subit davantage", "shield", "vulnerab", "increased damage"]
            ],
            ["avant", "rapidement", "grandement", "puissant", "before", "quickly", "greatly", "powerful"],
            Threshold: 1),
        new RoleWording("Damage"));
}
