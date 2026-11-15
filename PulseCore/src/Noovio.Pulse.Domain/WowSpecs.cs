using Noovio.Pulse.Domain.Enums;
using Noovio.Pulse.Domain.Resources;
using Noovio.Pulse.Domain.Roles;

namespace Noovio.Pulse.Domain;

/// <summary>Fiche descriptive d'une spécialisation (classe, couleur et icône WoW). Les libellés suivent la langue de l'interface.</summary>
/// <param name="Basis">Lignes de la feuille de personnage qui font foi, quand elles diffèrent de celles du rôle (DPS à distance, lanceurs de sorts).</param>
public sealed record SpecInfo(WowSpec Spec, string Slug, int ClassId, string Color, string IconUrl, StatBasis? Basis = null)
{
    /// <summary>« Gardien » / « Guardian », dans la langue de l'interface.</summary>
    public string SpecName => GameTexts.ResourceManager.GetString($"Spec_{Spec}", GameTexts.Culture) ?? Spec.ToString();

    /// <summary>« Druide » / « Druid », dans la langue de l'interface.</summary>
    public string ClassName => WowClasses.Name(ClassId);
}

/// <summary>Les classes jouables, par identifiant Blizzard (<c>playable-class</c>).</summary>
public static class WowClasses
{
    /// <summary>Nom de la classe dans la langue de l'interface ; vide pour un identifiant inconnu.</summary>
    public static string Name(int classId) => GameTexts.ResourceManager.GetString($"Class_{classId}", GameTexts.Culture) ?? "";
}

public static class WowSpecs
{
    /// <summary>Icônes : <c>/data/wow/media/playable-specialization/{id}</c>, figées ici pour ne pas dépendre d'une ingestion.</summary>
    private const string Icons = "https://render.worldofwarcraft.com/us/icons/56/";

    public static readonly IReadOnlyList<SpecInfo> Tanks =
    [
        new(WowSpec.BloodDeathKnight, "blood", 6, "#C41E3A", Icons + "135770.jpg"),
        new(WowSpec.VengeanceDemonHunter, "vengeance", 12, "#A330C9", Icons + "1247265.jpg"),
        new(WowSpec.GuardianDruid, "guardian", 11, "#FF7C0A", Icons + "132276.jpg"),
        new(WowSpec.BrewmasterMonk, "brewmaster", 10, "#00FF98", Icons + "608951.jpg"),
        new(WowSpec.ProtectionPaladin, "protection-paladin", 2, "#F48CBA", Icons + "236264.jpg"),
        new(WowSpec.ProtectionWarrior, "protection-warrior", 1, "#C69B6D", Icons + "132341.jpg")
    ];

    public static readonly IReadOnlyList<SpecInfo> Healers =
    [
        new(WowSpec.RestorationShaman, "restoration-shaman", 7, "#0070DD", Icons + "136052.jpg"),
        new(WowSpec.RestorationDruid, "restoration-druid", 11, "#FF7C0A", Icons + "136041.jpg"),
        new(WowSpec.PreservationEvoker, "preservation", 13, "#33937F", Icons + "4511812.jpg"),
        new(WowSpec.MistweaverMonk, "mistweaver", 10, "#00FF98", Icons + "608952.jpg"),
        new(WowSpec.HolyPaladin, "holy-paladin", 2, "#F48CBA", Icons + "135920.jpg"),
        new(WowSpec.DisciplinePriest, "discipline", 5, "#FFFFFF", Icons + "135940.jpg"),
        new(WowSpec.HolyPriest, "holy-priest", 5, "#FFFFFF", Icons + "237542.jpg")
    ];

    /// <summary>
    /// Par classe : la navigation les regroupe ainsi, triées dans la langue de l'interface.
    /// Le mêlée lit les lignes de mêlée (valeur du rôle), les chasseurs à distance les lignes à distance, les lanceurs de sorts celles de sort.
    /// </summary>
    public static readonly IReadOnlyList<SpecInfo> Damage =
    [
        new(WowSpec.ElementalShaman, "elemental", 7, "#0070DD", Icons + "136048.jpg", StatBasis.Spell),
        new(WowSpec.EnhancementShaman, "enhancement", 7, "#0070DD", Icons + "237581.jpg"),
        new(WowSpec.BeastMasteryHunter, "beast-mastery", 3, "#AAD372", Icons + "461112.jpg", StatBasis.Ranged),
        new(WowSpec.MarksmanshipHunter, "marksmanship", 3, "#AAD372", Icons + "236179.jpg", StatBasis.Ranged),
        new(WowSpec.SurvivalHunter, "survival", 3, "#AAD372", Icons + "461113.jpg"),
        new(WowSpec.HavocDemonHunter, "havoc", 12, "#A330C9", Icons + "1247264.jpg"),
        new(WowSpec.DevourerDemonHunter, "devourer", 12, "#A330C9", Icons + "7455385.jpg", StatBasis.Spell),
        new(WowSpec.FrostDeathKnight, "frost-death-knight", 6, "#C41E3A", Icons + "135773.jpg"),
        new(WowSpec.UnholyDeathKnight, "unholy", 6, "#C41E3A", Icons + "135775.jpg"),
        new(WowSpec.AfflictionWarlock, "affliction", 9, "#8788EE", Icons + "136145.jpg", StatBasis.Spell),
        new(WowSpec.DemonologyWarlock, "demonology", 9, "#8788EE", Icons + "136172.jpg", StatBasis.Spell),
        new(WowSpec.DestructionWarlock, "destruction", 9, "#8788EE", Icons + "136186.jpg", StatBasis.Spell),
        new(WowSpec.BalanceDruid, "balance", 11, "#FF7C0A", Icons + "136096.jpg", StatBasis.Spell),
        new(WowSpec.FeralDruid, "feral", 11, "#FF7C0A", Icons + "132115.jpg"),
        new(WowSpec.DevastationEvoker, "devastation", 13, "#33937F", Icons + "4511811.jpg", StatBasis.Spell),
        new(WowSpec.AugmentationEvoker, "augmentation", 13, "#33937F", Icons + "5198700.jpg", StatBasis.Spell),
        new(WowSpec.ArmsWarrior, "arms", 1, "#C69B6D", Icons + "132355.jpg"),
        new(WowSpec.FuryWarrior, "fury", 1, "#C69B6D", Icons + "132347.jpg"),
        new(WowSpec.ArcaneMage, "arcane", 8, "#3FC7EB", Icons + "135932.jpg", StatBasis.Spell),
        new(WowSpec.FireMage, "fire", 8, "#3FC7EB", Icons + "135810.jpg", StatBasis.Spell),
        new(WowSpec.FrostMage, "frost-mage", 8, "#3FC7EB", Icons + "135846.jpg", StatBasis.Spell),
        new(WowSpec.WindwalkerMonk, "windwalker", 10, "#00FF98", Icons + "608953.jpg"),
        new(WowSpec.RetributionPaladin, "retribution", 2, "#F48CBA", Icons + "135873.jpg"),
        new(WowSpec.ShadowPriest, "shadow", 5, "#FFFFFF", Icons + "136207.jpg", StatBasis.Spell),
        new(WowSpec.AssassinationRogue, "assassination", 4, "#FFF468", Icons + "236270.jpg"),
        new(WowSpec.OutlawRogue, "outlaw", 4, "#FFF468", Icons + "236286.jpg"),
        new(WowSpec.SubtletyRogue, "subtlety", 4, "#FFF468", Icons + "132320.jpg")
    ];

    public static SpecInfo Info(this WowSpec spec) => Tanks.Concat(Healers).Concat(Damage).First(s => s.Spec == spec);
}
