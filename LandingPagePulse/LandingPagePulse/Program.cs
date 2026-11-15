using System.Globalization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using LandingPagePulse.Components;
using LandingPagePulse.Components.Shared;

var builder = WebApplication.CreateBuilder(args);

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

// Comme AltPulse : Nginx termine le HTTPS et relaie en HTTP sur 127.0.0.1.
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

// Une redirection par outil (/altpulse, /frontpulse, /lifepulse, /burstpulse, et /addon pour télécharger NoovioPulse) :
// les adresses vivent dans la configuration.
// 302 et non 301 : un navigateur ne mémorise pas la cible, qui peut donc changer sans laisser de trace.
foreach (var (slug, url) in app.Configuration.GetRequiredSection("Tools").Get<Dictionary<string, string>>()!)
{
    app.MapGet($"/{slug}", () => Results.Redirect(url));
}

// GET /culture/en?returnUrl=/ : enregistre le choix de langue et revient sur la page. Le cookie est partagé
// entre les sites Noovio (Localization:CookieDomain) : une langue choisie ici vaut pour les outils.
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
app.MapRazorComponents<App>();

app.Run();
