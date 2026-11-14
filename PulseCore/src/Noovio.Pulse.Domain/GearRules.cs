using Noovio.Pulse.Domain.Entities;
using Noovio.Pulse.Domain.Enums;

namespace Noovio.Pulse.Domain;

/// <summary>Règles d'équipement communes aux profils lus chez Blizzard et aux imports de l'addon.</summary>
public static class GearRules
{
    /// <summary>Type d'emplacement Blizzard (« HEAD », « FINGER_1 »…) ; null pour la chemise, le tabard ou un type inconnu.</summary>
    public static GearSlot? SlotOf(string? type) => type switch
    {
        "HEAD" => GearSlot.Head,
        "NECK" => GearSlot.Neck,
        "SHOULDER" => GearSlot.Shoulder,
        "BACK" => GearSlot.Back,
        "CHEST" => GearSlot.Chest,
        "WRIST" => GearSlot.Wrist,
        "HANDS" => GearSlot.Hands,
        "WAIST" => GearSlot.Waist,
        "LEGS" => GearSlot.Legs,
        "FEET" => GearSlot.Feet,
        "FINGER_1" or "FINGER_2" => GearSlot.Finger,
        "TRINKET_1" or "TRINKET_2" => GearSlot.Trinket,
        "MAIN_HAND" => GearSlot.MainHand,
        "OFF_HAND" => GearSlot.OffHand,
        _ => null
    };

    /// <summary>Comme le jeu : 16 emplacements, une arme à deux mains compte deux fois.</summary>
    public static double AverageItemLevel(IReadOnlyList<GearPiece> gear)
    {
        var total = gear.Sum(g => g.ItemLevel);
        if (gear.FirstOrDefault(g => g.Slot == GearSlot.MainHand) is { } mainHand && gear.All(g => g.Slot != GearSlot.OffHand))
        {
            total += mainHand.ItemLevel;
        }

        return Math.Round(total / 16.0, 1);
    }

    /// <summary>« Unique-Equipped: Embellished (2) » en anglais, « Unique - Équipé : Enjolivement (2) » en français.</summary>
    public static bool IsEmbellished(string? limitCategory) =>
        limitCategory is not null
        && (limitCategory.Contains("Embellished", StringComparison.OrdinalIgnoreCase)
            || limitCategory.Contains("Enjolivement", StringComparison.OrdinalIgnoreCase));
}
