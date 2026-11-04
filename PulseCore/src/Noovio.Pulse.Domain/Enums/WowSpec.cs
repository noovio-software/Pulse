namespace Noovio.Pulse.Domain.Enums;

/// <summary>
/// Spécialisations suivies par les applications Pulse. Les valeurs sont les identifiants Blizzard des spécialisations,
/// repris tels quels par Raider.io (<c>roster[].character.spec.id</c>) : aucune ambiguïté
/// entre les deux « Protection », « Sacré » ou « Restauration ».
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
    PreservationEvoker = 1468
}
