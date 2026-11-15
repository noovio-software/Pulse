using System.Globalization;

namespace LandingPagePulse.Components.Shared;

/// <summary>Langues de la page (français par défaut, anglais) et petits outils de mise en forme.</summary>
public static class Ui
{
    public const string DefaultCulture = "fr";

    /// <summary>Cultures neutres : « en-GB » ou « fr-CA » du navigateur retombent sur leur langue.</summary>
    public static readonly IReadOnlyList<string> Cultures = [DefaultCulture, "en"];

    /// <summary>« fr » ou « en » : la langue de la requête en cours.</summary>
    public static string CurrentLanguage => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

    /// <summary>Texte à paramètres dans la culture de la requête.</summary>
    public static string F(string format, params object?[] args) => string.Format(CultureInfo.CurrentCulture, format, args);

    /// <summary>L'adresse de bascule vers <paramref name="culture"/> depuis la page <paramref name="relativeUrl"/> (sans « / » initial).</summary>
    public static string SwitchUrl(string culture, string relativeUrl) =>
        $"culture/{culture}?returnUrl={Uri.EscapeDataString("/" + relativeUrl.TrimStart('/'))}";

    /// <summary>Le chemin de retour s'il reste sur le site (ni adresse externe ni « //hôte »), sinon l'accueil.</summary>
    public static string SafeReturnUrl(string? url) =>
        url is { Length: > 0 } && url[0] == '/' && (url.Length == 1 || (url[1] != '/' && url[1] != '\\')) ? url : "/";
}
