namespace Noovio.Pulse.Web;

/// <summary>L'addon NoovioPulse vu du site : où le télécharger, et combien de temps un import reste consultable.</summary>
public static class AddonInfo
{
    public const string DownloadUrl = "https://github.com/noovio-software/Pulse/releases?q=noovio-pulse-addon&expanded=true";

    public static readonly TimeSpan Retention = TimeSpan.FromDays(7);

    public static string CacheKey(string key) => $"import:{key}";
}
