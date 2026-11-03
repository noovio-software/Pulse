using Noovio.FrontPulse.Domain.Enums;

namespace Noovio.FrontPulse.Domain;

/// <summary>Fiche descriptive d'une spécialisation tank (libellés, classe, couleur et icône WoW).</summary>
public sealed record TankSpecInfo(TankSpec Spec, string Slug, string SpecName, string ClassName, int ClassId, string Color, string IconUrl);

public static class TankSpecs
{
    /// <summary>Icônes : <c>/data/wow/media/playable-specialization/{id}</c>, figées ici pour ne pas dépendre d'une ingestion.</summary>
    private const string Icons = "https://render.worldofwarcraft.com/us/icons/56/";

    public static readonly IReadOnlyList<TankSpecInfo> All =
    [
        new(TankSpec.BloodDeathKnight, "blood", "Sang", "Chevalier de la mort", 6, "#C41E3A", Icons + "135770.jpg"),
        new(TankSpec.VengeanceDemonHunter, "vengeance", "Vengeance", "Chasseur de démons", 12, "#A330C9", Icons + "1247265.jpg"),
        new(TankSpec.GuardianDruid, "guardian", "Gardien", "Druide", 11, "#FF7C0A", Icons + "132276.jpg"),
        new(TankSpec.BrewmasterMonk, "brewmaster", "Maître brasseur", "Moine", 10, "#00FF98", Icons + "608951.jpg"),
        new(TankSpec.ProtectionPaladin, "protection-paladin", "Protection", "Paladin", 2, "#F48CBA", Icons + "236264.jpg"),
        new(TankSpec.ProtectionWarrior, "protection-warrior", "Protection", "Guerrier", 1, "#C69B6D", Icons + "132341.jpg")
    ];

    public static TankSpecInfo Info(this TankSpec spec) => All.First(s => s.Spec == spec);

    public static TankSpecInfo? FromSlug(string? slug) =>
        All.FirstOrDefault(s => string.Equals(s.Slug, slug, StringComparison.OrdinalIgnoreCase));

    public static bool IsTank(int specId) => Enum.IsDefined(typeof(TankSpec), specId);

    /// <summary>La spécialisation tank d'une classe (une seule par classe), ou null si la classe n'en a pas.</summary>
    public static TankSpecInfo? ForClass(int classId) => All.FirstOrDefault(s => s.ClassId == classId);
}
