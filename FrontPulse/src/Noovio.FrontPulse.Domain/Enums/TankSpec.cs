namespace Noovio.FrontPulse.Domain.Enums;

/// <summary>
/// Spécialisations tank. Les valeurs sont les identifiants Blizzard des spécialisations,
/// repris tels quels par Raider.io (<c>roster[].character.spec.id</c>) : aucune ambiguïté
/// entre les deux « Protection ».
/// </summary>
public enum TankSpec
{
    ProtectionWarrior = 73,
    ProtectionPaladin = 66,
    BloodDeathKnight = 250,
    GuardianDruid = 104,
    BrewmasterMonk = 268,
    VengeanceDemonHunter = 581
}
