using Noovio.Pulse.Domain.Enums;

namespace Noovio.Pulse.Domain;

/// <summary>Fiche descriptive d'une spécialisation (libellés, classe, couleur et icône WoW).</summary>
public sealed record SpecInfo(WowSpec Spec, string Slug, string SpecName, string ClassName, int ClassId, string Color, string IconUrl);

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

    public static SpecInfo Info(this WowSpec spec) => Tanks.Concat(Healers).First(s => s.Spec == spec);
}
