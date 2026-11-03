namespace Noovio.FrontPulse.Web.Components.Layout;

public partial class MainLayout
{
    /// <summary>Version de l'assembly ramenée à ses trois premiers nombres (voir le .csproj).</summary>
    private static readonly string AppVersion =
        string.Join('.', (typeof(MainLayout).Assembly.GetName().Version?.ToString() ?? "0.0.0").Split('.').Take(3));
}
