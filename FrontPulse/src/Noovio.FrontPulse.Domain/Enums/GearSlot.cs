namespace Noovio.FrontPulse.Domain.Enums;

/// <summary>Emplacements agrégés : les deux anneaux et les deux bijoux sont regroupés.</summary>
public enum GearSlot
{
    Head,
    Neck,
    Shoulder,
    Back,
    Chest,
    Wrist,
    Hands,
    Waist,
    Legs,
    Feet,
    Finger,
    Trinket,
    MainHand,
    OffHand
}

public enum TalentTree
{
    Class,
    Spec,
    Hero
}

/// <summary>Forme du nœud en jeu : carré (actif), rond (passif), octogone (choix).</summary>
public enum TalentNodeKind
{
    Active,
    Passive,
    Choice
}

public enum TrinketOrientation
{
    Defensive,
    Offensive
}
