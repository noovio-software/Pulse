using System.Globalization;
using Microsoft.AspNetCore.Localization;

namespace Noovio.Pulse.Web.Localization;

/// <summary>
/// Langues du site : français (par défaut) et anglais. La langue vient du cookie de choix, sinon du navigateur
/// (<c>Accept-Language</c>). Le cookie peut être partagé entre les sites Noovio (<c>Localization:CookieDomain</c>) :
/// une langue choisie sur l'un vaut pour tous.
/// </summary>
public static class PulseLocalization
{
    public const string DefaultCulture = "fr";

    /// <summary>Cultures neutres : « en-GB » ou « fr-CA » du navigateur retombent sur leur langue.</summary>
    public static readonly IReadOnlyList<string> Cultures = [DefaultCulture, "en"];

    public static IServiceCollection AddPulseLocalization(this IServiceCollection services) =>
        services.Configure<RequestLocalizationOptions>(options =>
        {
            options.SetDefaultCulture(DefaultCulture)
                .AddSupportedCultures([.. Cultures])
                .AddSupportedUICultures([.. Cultures]);
            // Le choix explicite d'abord, puis le navigateur. Pas de paramètre d'URL : les adresses restent stables.
            options.RequestCultureProviders = [new CookieRequestCultureProvider(), new AcceptLanguageHeaderRequestCultureProvider()];
            options.ApplyCurrentCultureToResponseHeaders = true;
        });

    /// <summary><c>GET /culture/{culture}?returnUrl=/specs/blood</c> : enregistre le choix et revient sur la page.</summary>
    public static IEndpointRouteBuilder MapCultureEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/culture/{culture}", (string culture, string? returnUrl, HttpContext http, IConfiguration configuration) =>
        {
            if (!Cultures.Contains(culture, StringComparer.OrdinalIgnoreCase))
            {
                return Results.NotFound();
            }

            http.Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture.ToLowerInvariant())),
                new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddYears(1),
                    Domain = configuration["Localization:CookieDomain"] is { Length: > 0 } domain ? domain : null,
                    Path = "/",
                    // Une préférence d'affichage, pas un traceur : elle n'a pas besoin de consentement.
                    IsEssential = true,
                    SameSite = SameSiteMode.Lax,
                    Secure = http.Request.IsHttps
                });

            return Results.LocalRedirect(SafeReturnUrl(returnUrl));
        });

        return app;
    }

    /// <summary>L'adresse de bascule vers <paramref name="culture"/> depuis la page <paramref name="relativeUrl"/> (sans « / » initial).</summary>
    public static string SwitchUrl(string culture, string relativeUrl) =>
        $"culture/{culture}?returnUrl={Uri.EscapeDataString("/" + relativeUrl.TrimStart('/'))}";

    /// <summary>« fr » ou « en » : la langue de la requête en cours.</summary>
    public static string CurrentLanguage => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

    /// <summary>Le chemin de retour s'il reste sur le site (ni adresse externe ni « //hôte »), sinon l'accueil.</summary>
    public static string SafeReturnUrl(string? url) =>
        url is { Length: > 0 } && url[0] == '/' && (url.Length == 1 || (url[1] != '/' && url[1] != '\\')) ? url : "/";
}
