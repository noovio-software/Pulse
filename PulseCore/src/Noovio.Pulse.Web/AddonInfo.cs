namespace Noovio.Pulse.Web;

/// <summary>L'addon NoovioPulse vu du site : où le télécharger, et combien de temps un import reste consultable.</summary>
public static class AddonInfo
{
    public const string CurseForgeUrl = "https://www.curseforge.com/wow/addons/nooviopulse";

    /// <summary>Téléchargement direct : les releases GitHub, pour qui n'utilise pas CurseForge.</summary>
    public const string DirectDownloadUrl = "https://github.com/noovio-software/Pulse/releases";

    public static readonly TimeSpan Retention = TimeSpan.FromDays(7);

    public static string CacheKey(string key) => $"import:{key}";
}
