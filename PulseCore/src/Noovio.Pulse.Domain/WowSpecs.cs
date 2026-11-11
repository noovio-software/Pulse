using Noovio.Pulse.Domain.Enums;
using Noovio.Pulse.Domain.Roles;

namespace Noovio.Pulse.Domain;

/// <summary>Fiche descriptive d'une spécialisation (libellés, classe, couleur et icône WoW).</summary>
/// <param name="Basis">Lignes de la feuille de personnage qui font foi, quand elles diffèrent de celles du rôle (DPS à distance, lanceurs de sorts).</param>
public sealed record SpecInfo(WowSpec Spec, string Slug, string SpecName, string ClassName, int ClassId, string Color, string IconUrl, StatBasis? Basis = null);

public static class WowSpecs
{
    /// <summary>Icônes : <c>/data/wow/media/playable-specialization/{id}</c>, figées ici pour ne pas dépendre d'une ingestion.</summary>
    private const string Icons = "https://render.worldofwarcraft.com/us/icons/56/";

    public static readonly IReadOnlyList<SpecInfo> Tanks =
    [
        new(WowSpec.BloodDeathKnight, "blood", "Sang", "Chevalier de la mort", 6, "#C41E3A", Icons + "135770.jpg"),
        new(WowSpec.VengeanceDemonHunter, "vengeance", "Vengeance", "Chasseur de démons", 12, "#A330C9", Icons + "1247265.jpg"),
        new(WowSpec.GuardianDruid, "guardian", "Gardien", "Druide", 11, "#FF7C0A", Icons + "132276.jpg"),
        new(WowSpec.BrewmasterMonk, "brewmaster", "Maître brasseur", "Moine", 10, "#00FF98", Icons + "608951.jpg"),
        new(WowSpec.ProtectionPaladin, "protection-paladin", "Protection", "Paladin", 2, "#F48CBA", Icons + "236264.jpg"),
        new(WowSpec.ProtectionWarrior, "protection-warrior", "Protection", "Guerrier", 1, "#C69B6D", Icons + "132341.jpg")
    ];

    public static readonly IReadOnlyList<SpecInfo> Healers =
    [
        new(WowSpec.RestorationShaman, "restoration-shaman", "Restauration", "Chaman", 7, "#0070DD", Icons + "136052.jpg"),
        new(WowSpec.RestorationDruid, "restoration-druid", "Restauration", "Druide", 11, "#FF7C0A", Icons + "136041.jpg"),
        new(WowSpec.PreservationEvoker, "preservation", "Préservation", "Évocateur", 13, "#33937F", Icons + "4511812.jpg"),
        new(WowSpec.MistweaverMonk, "mistweaver", "Tisse-brume", "Moine", 10, "#00FF98", Icons + "608952.jpg"),
        new(WowSpec.HolyPaladin, "holy-paladin", "Sacré", "Paladin", 2, "#F48CBA", Icons + "135920.jpg"),
        new(WowSpec.DisciplinePriest, "discipline", "Discipline", "Prêtre", 5, "#FFFFFF", Icons + "135940.jpg"),
        new(WowSpec.HolyPriest, "holy-priest", "Sacré", "Prêtre", 5, "#FFFFFF", Icons + "237542.jpg")
    ];

    /// <summary>
    /// Par classe, dans l'ordre alphabétique français : c'est ainsi que la navigation les regroupe.
    /// Le mêlée lit les lignes de mêlée (valeur du rôle), les chasseurs à distance les lignes à distance, les lanceurs de sorts celles de sort.
    /// </summary>
    public static readonly IReadOnlyList<SpecInfo> Damage =
    [
        new(WowSpec.ElementalShaman, "elemental", "Élémentaire", "Chaman", 7, "#0070DD", Icons + "136048.jpg", StatBasis.Spell),
        new(WowSpec.EnhancementShaman, "enhancement", "Amélioration", "Chaman", 7, "#0070DD", Icons + "237581.jpg"),
        new(WowSpec.BeastMasteryHunter, "beast-mastery", "Maîtrise des bêtes", "Chasseur", 3, "#AAD372", Icons + "461112.jpg", StatBasis.Ranged),
        new(WowSpec.MarksmanshipHunter, "marksmanship", "Précision", "Chasseur", 3, "#AAD372", Icons + "236179.jpg", StatBasis.Ranged),
        new(WowSpec.SurvivalHunter, "survival", "Survie", "Chasseur", 3, "#AAD372", Icons + "461113.jpg"),
        new(WowSpec.HavocDemonHunter, "havoc", "Dévastation", "Chasseur de démons", 12, "#A330C9", Icons + "1247264.jpg"),
        new(WowSpec.DevourerDemonHunter, "devourer", "Dévoration", "Chasseur de démons", 12, "#A330C9", Icons + "7455385.jpg", StatBasis.Spell),
        new(WowSpec.FrostDeathKnight, "frost-death-knight", "Givre", "Chevalier de la mort", 6, "#C41E3A", Icons + "135773.jpg"),
        new(WowSpec.UnholyDeathKnight, "unholy", "Impie", "Chevalier de la mort", 6, "#C41E3A", Icons + "135775.jpg"),
        new(WowSpec.AfflictionWarlock, "affliction", "Affliction", "Démoniste", 9, "#8788EE", Icons + "136145.jpg", StatBasis.Spell),
        new(WowSpec.DemonologyWarlock, "demonology", "Démonologie", "Démoniste", 9, "#8788EE", Icons + "136172.jpg", StatBasis.Spell),
        new(WowSpec.DestructionWarlock, "destruction", "Destruction", "Démoniste", 9, "#8788EE", Icons + "136186.jpg", StatBasis.Spell),
        new(WowSpec.BalanceDruid, "balance", "Équilibre", "Druide", 11, "#FF7C0A", Icons + "136096.jpg", StatBasis.Spell),
        new(WowSpec.FeralDruid, "feral", "Farouche", "Druide", 11, "#FF7C0A", Icons + "132115.jpg"),
        new(WowSpec.DevastationEvoker, "devastation", "Dévastation", "Évocateur", 13, "#33937F", Icons + "4511811.jpg", StatBasis.Spell),
        new(WowSpec.AugmentationEvoker, "augmentation", "Augmentation", "Évocateur", 13, "#33937F", Icons + "5198700.jpg", StatBasis.Spell),
        new(WowSpec.ArmsWarrior, "arms", "Armes", "Guerrier", 1, "#C69B6D", Icons + "132355.jpg"),
        new(WowSpec.FuryWarrior, "fury", "Fureur", "Guerrier", 1, "#C69B6D", Icons + "132347.jpg"),
        new(WowSpec.ArcaneMage, "arcane", "Arcanes", "Mage", 8, "#3FC7EB", Icons + "135932.jpg", StatBasis.Spell),
        new(WowSpec.FireMage, "fire", "Feu", "Mage", 8, "#3FC7EB", Icons + "135810.jpg", StatBasis.Spell),
        new(WowSpec.FrostMage, "frost-mage", "Givre", "Mage", 8, "#3FC7EB", Icons + "135846.jpg", StatBasis.Spell),
        new(WowSpec.WindwalkerMonk, "windwalker", "Marche-vent", "Moine", 10, "#00FF98", Icons + "608953.jpg"),
        new(WowSpec.RetributionPaladin, "retribution", "Vindicte", "Paladin", 2, "#F48CBA", Icons + "135873.jpg"),
        new(WowSpec.ShadowPriest, "shadow", "Ombre", "Prêtre", 5, "#FFFFFF", Icons + "136207.jpg", StatBasis.Spell),
        new(WowSpec.AssassinationRogue, "assassination", "Assassinat", "Voleur", 4, "#FFF468", Icons + "236270.jpg"),
        new(WowSpec.OutlawRogue, "outlaw", "Hors-la-loi", "Voleur", 4, "#FFF468", Icons + "236286.jpg"),
        new(WowSpec.SubtletyRogue, "subtlety", "Finesse", "Voleur", 4, "#FFF468", Icons + "132320.jpg")
    ];

    public static SpecInfo Info(this WowSpec spec) => Tanks.Concat(Healers).Concat(Damage).First(s => s.Spec == spec);
}
