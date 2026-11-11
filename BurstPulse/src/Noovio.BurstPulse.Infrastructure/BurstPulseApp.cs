using Noovio.Pulse.Domain.Roles;

namespace Noovio.BurstPulse.Infrastructure;

public static class BurstPulseApp
{
    /// <summary>Emblème : une étoile d'explosion à huit branches traversée d'un pouls, même tracé que wwwroot/favicon.svg.</summary>
    public static readonly PulseApp Definition = new("BurstPulse", PulseRoles.Damage, "M16 2 18.9 9 25.9 6.1 23 13.1 30 16 23 18.9 25.9 25.9 18.9 23 16 30 13.1 23 6.1 25.9 9 18.9 2 16 9 13.1 6.1 6.1 13.1 9z");
}
