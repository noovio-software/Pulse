using System.Globalization;
using System.Runtime.CompilerServices;

namespace Noovio.Pulse.Tests;

/// <summary>
/// Les tests vérifient des textes français : la culture est fixée au chargement, quelle que soit celle de la machine (CI en anglais).
/// Un test qui vérifie l'anglais passe par <see cref="Use"/>.
/// </summary>
internal static class TestCulture
{
    [ModuleInitializer]
    internal static void FrenchByDefault() =>
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("fr");

    /// <summary>Bascule la culture du test, et la rétablit à la libération.</summary>
    public static IDisposable Use(string culture)
    {
        var (previous, previousUi) = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo(culture);
        return new Restore(() => (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = (previous, previousUi));
    }

    private sealed class Restore(Action restore) : IDisposable
    {
        public void Dispose() => restore();
    }
}
