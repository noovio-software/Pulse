using System.Globalization;

namespace AltPulse.Client.Localization;

/// <summary>
/// Langues d'AltPulse : français (par défaut) et anglais. Côté serveur, la langue vient du cookie de choix, sinon du navigateur ;
/// côté WebAssembly, de l'attribut <c>lang</c> de la page prérendue (voir Program.cs) : les deux rendus parlent la même langue.
/// </summary>
public static class AltLocalization
{
    public const string DefaultCulture = "fr";

    /// <summary>Cultures neutres : « en-GB » ou « fr-CA » du navigateur retombent sur leur langue.</summary>
    public static readonly IReadOnlyList<string> Cultures = [DefaultCulture, "en"];

    /// <summary>« fr » ou « en » : la langue de l'interface en cours.</summary>
    public static string CurrentLanguage => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

    public static bool IsEnglish => CurrentLanguage == "en";

    /// <summary>La culture prise en charge la plus proche de <paramref name="name"/> (« en-US » → « en »), sinon le français.</summary>
    public static string Resolve(string? name) =>
        Cultures.FirstOrDefault(c => name is not null && name.StartsWith(c, StringComparison.OrdinalIgnoreCase)) ?? DefaultCulture;

    /// <summary>L'adresse de bascule vers <paramref name="culture"/> depuis la page <paramref name="relativeUrl"/> (sans « / » initial).</summary>
    public static string SwitchUrl(string culture, string relativeUrl) =>
        $"culture/{culture}?returnUrl={Uri.EscapeDataString("/" + relativeUrl.TrimStart('/'))}";

    /// <summary>Le chemin de retour s'il reste sur le site (ni adresse externe ni « //hôte »), sinon l'accueil.</summary>
    public static string SafeReturnUrl(string? url) =>
        url is { Length: > 0 } && url[0] == '/' && (url.Length == 1 || (url[1] != '/' && url[1] != '\\')) ? url : "/";

    /// <summary>Texte à paramètres dans la culture de l'interface.</summary>
    public static string F(string format, params object?[] args) => string.Format(CultureInfo.CurrentCulture, format, args);

    /// <summary>Singulier ou pluriel selon la règle de la langue : 0 et 1 au singulier en français, seulement 1 en anglais.</summary>
    public static string Plural(int count, string one, string other) =>
        F(count == 1 || (count == 0 && !IsEnglish) ? one : other, count);

    /// <summary>Site Wowhead dans la langue de l'interface, et paramètre <c>domain</c> des infobulles.</summary>
    public static string WowheadSite => IsEnglish ? "https://www.wowhead.com" : "https://www.wowhead.com/fr";

    public static string WowheadDomain => IsEnglish ? "" : "&domain=fr";

    /// <summary>Langue de l'armurerie Blizzard pour la région du personnage.</summary>
    public static string ArmoryLocale(string region) =>
        IsEnglish ? region.Equals("us", StringComparison.OrdinalIgnoreCase) ? "en-us" : "en-gb" : "fr-fr";
}
