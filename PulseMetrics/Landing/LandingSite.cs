using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using PulseMetrics.Landing.Components;
using PulseMetrics.Landing.Components.Pages;
using PulseMetrics.Landing.Components.Shared;
using Microsoft.AspNetCore.Http.HttpResults;
using NotFoundPage = PulseMetrics.Landing.Components.Pages.NotFound;

namespace PulseMetrics.Landing;

/// <summary>Page d'accueil de PulseMetrics, à la racine : présente les outils et redirige les anciennes adresses.</summary>
public static class LandingSite
{
    public static WebApplication Build(WebApplicationBuilder builder)
    {
        // Hors requête : français, comme la page par défaut.
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo(Ui.DefaultCulture);

        // Langue : le choix explicite (cookie partagé avec les outils) d'abord, puis le navigateur. Pas de paramètre d'URL.
        builder.Services.Configure<RequestLocalizationOptions>(options =>
        {
            options.SetDefaultCulture(Ui.DefaultCulture)
                .AddSupportedCultures([.. Ui.Cultures])
                .AddSupportedUICultures([.. Ui.Cultures]);
            options.RequestCultureProviders = [new CookieRequestCultureProvider(), new AcceptLanguageHeaderRequestCultureProvider()];
            options.ApplyCurrentCultureToResponseHeaders = true;
        });

        // Rendu statique côté serveur : la page n'a besoin ni de circuit, ni de WebAssembly.
        builder.Services.AddRazorComponents();

        // Nginx termine le HTTPS et relaie en HTTP sur 127.0.0.1.
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);

        var app = builder.Build();

        app.UseForwardedHeaders();
        app.UseRequestLocalization();

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error", createScopeForErrors: true);
            app.UseHsts();
        }
        app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
        app.UseHttpsRedirection();
        app.UseAntiforgery();

        // Redirections de la configuration (Redirects) : téléchargement de l'addon (/addon, /addon-direct) et anciennes
        // adresses des outils (/altpulse…, reçues de noovio.fr). 302 et non 301 : un navigateur ne mémorise pas la cible,
        // qui peut donc changer sans laisser de trace.
        foreach (var (slug, url) in app.Configuration.GetSection("Redirects").Get<Dictionary<string, string>>() ?? [])
        {
            app.MapGet($"/{slug}", () => Results.Redirect(url));
        }

        // GET /culture/en?returnUrl=/ : enregistre le choix de langue et revient sur la page. Le cookie vaut pour tout
        // le domaine : une langue choisie ici vaut pour les outils.
        app.MapGet("/culture/{culture}", (string culture, string? returnUrl, HttpContext http, IConfiguration configuration) =>
        {
            if (!Ui.Cultures.Contains(culture, StringComparer.OrdinalIgnoreCase))
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

            return Results.LocalRedirect(Ui.SafeReturnUrl(returnUrl));
        });

        app.MapStaticAssets();

        // Trois pages, sans routeur Blazor (voir App.Page). /not-found et /Error sont rejouées par les middlewares
        // ci-dessus quelle que soit la méthode : elles gardent le code d'origine (404, 500).
        app.MapGet("/", () => Page<Home>());
        app.Map("/not-found", () => Page<NotFoundPage>());
        app.Map("/Error", () => Page<Error>());

        return app;
    }

    private static RazorComponentResult<App> Page<TPage>() where TPage : IComponent =>
        new(new Dictionary<string, object?> { [nameof(App.Page)] = typeof(TPage) });
}
