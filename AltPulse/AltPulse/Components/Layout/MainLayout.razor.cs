namespace AltPulse.Components.Layout;

public partial class MainLayout
{
    /// <summary>
    /// Version de l'assembly (1.2.3.0), gravée à la compilation depuis le tag GitHub
    /// (voir AltPulse.csproj), ramenée à ses trois premiers nombres.
    /// </summary>
    private static readonly string AppVersion =
        string.Join('.', (typeof(MainLayout).Assembly.GetName().Version?.ToString() ?? "0.0.0").Split('.').Take(3));
}
