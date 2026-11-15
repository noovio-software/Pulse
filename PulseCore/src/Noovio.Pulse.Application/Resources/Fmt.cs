using System.Globalization;

namespace Noovio.Pulse.Application.Resources;

/// <summary>Mise en forme des ressources dans la culture de l'interface (nombres, pourcentages).</summary>
internal static class Fmt
{
    public static string Of(string format, params object?[] args) => string.Format(CultureInfo.CurrentCulture, format, args);
}
