namespace Noovio.Pulse.Domain.Enums;

/// <summary>
/// Spécialisations suivies par les applications Pulse. Les valeurs sont les identifiants Blizzard des spécialisations,
/// repris tels quels par Raider.io (<c>roster[].character.spec.id</c>) : aucune ambiguïté
/// entre les deux « Protection », « Sacré », « Restauration » ou « Givre ».
/// </summary>
public enum WowSpec
{
    ProtectionWarrior = 73,
    ProtectionPaladin = 66,
    BloodDeathKnight = 250,
    GuardianDruid = 104,
    BrewmasterMonk = 268,
    VengeanceDemonHunter = 581,

    HolyPaladin = 65,
    DisciplinePriest = 256,
    HolyPriest = 257,
    RestorationShaman = 264,
    RestorationDruid = 105,
    MistweaverMonk = 270,
    PreservationEvoker = 1468,

    ElementalShaman = 262,
    EnhancementShaman = 263,
    BeastMasteryHunter = 253,
    MarksmanshipHunter = 254,
    SurvivalHunter = 255,
    HavocDemonHunter = 577,
    DevourerDemonHunter = 1480,
    FrostDeathKnight = 251,
    UnholyDeathKnight = 252,
    AfflictionWarlock = 265,
    DemonologyWarlock = 266,
    DestructionWarlock = 267,
    BalanceDruid = 102,
    FeralDruid = 103,
    DevastationEvoker = 1467,
    AugmentationEvoker = 1473,
    ArmsWarrior = 71,
    FuryWarrior = 72,
    ArcaneMage = 62,
    FireMage = 63,
    FrostMage = 64,
    WindwalkerMonk = 269,
    RetributionPaladin = 70,
    ShadowPriest = 258,
    AssassinationRogue = 259,
    OutlawRogue = 260,
    SubtletyRogue = 261
}
