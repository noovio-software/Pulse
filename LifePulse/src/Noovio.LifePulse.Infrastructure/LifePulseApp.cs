using Noovio.Pulse.Domain.Roles;

namespace Noovio.LifePulse.Infrastructure;

public static class LifePulseApp
{
    /// <summary>Emblème : une goutte traversée d'un pouls, même tracé que wwwroot/favicon.svg.</summary>
    public static readonly PulseApp Definition = new("LifePulse", PulseRoles.Healer, "M16 2.5c5.2 6.6 10.5 11.4 10.5 17.1a10.5 10.5 0 0 1-21 0C5.5 13.9 10.8 9.1 16 2.5z");
}
