using Noovio.Pulse.Domain.Roles;

namespace Noovio.FrontPulse.Infrastructure;

public static class FrontPulseApp
{
    /// <summary>Emblème : un écu traversé d'un pouls, même tracé que wwwroot/favicon.svg.</summary>
    public static readonly PulseApp Definition = new("FrontPulse", PulseRoles.Tank, "M16 3l10 4v8c0 6.5-4.2 11-10 14-5.8-3-10-7.5-10-14V7z")
    {
        HasDungeonRoutes = true
    };
}
