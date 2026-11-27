using System.Globalization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using PulseMetrics.Alt.Client.Localization;
using PulseMetrics.Alt.Client.Services;
using PulseMetrics.Alt.Components;
using PulseMetrics.Alt.Services;
using PulseMetrics.Alt.Services.Blizzard;
using PulseMetrics.Alt.Services.RaiderIo;

namespace PulseMetrics.Alt;

/// <summary>Alt (suivi des personnages), monté sous /alt/ : rendu serveur puis WebAssembly, API /api/* pour le client.</summary>
public static class AltSite
{
    public static WebApplication Build(WebApplicationBuilder builder)
    {
        // Hors requête (démarrage, tâches de fond) : français, comme le site par défaut.
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture =
            new CultureInfo(AltLocalization.DefaultCulture);

        // Langue : le choix explicite (cookie) d'abord, puis le navigateur. Pas de paramètre d'URL : les adresses restent stables.
        builder.Services.Configure<RequestLocalizationOptions>(options =>
        {
            options.SetDefaultCulture(AltLocalization.DefaultCulture)
                .AddSupportedCultures([.. AltLocalization.Cultures])
                .AddSupportedUICultures([.. AltLocalization.Cultures]);
            options.RequestCultureProviders = [new CookieRequestCultureProvider(), new AcceptLanguageHeaderRequestCultureProvider()];
            options.ApplyCurrentCultureToResponseHeaders = true;
        });

        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents()
            .AddInteractiveWebAssemblyComponents();

        // Nginx termine le HTTPS et relaie en HTTP sur 127.0.0.1.
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);

        builder.Services.AddMemoryCache();
        builder.Services.Configure<BlizzardOptions>(builder.Configuration.GetSection(BlizzardOptions.SectionName));
        builder.Services.AddHttpClient<BlizzardClient>(c => c.Timeout = TimeSpan.FromSeconds(15));
        builder.Services.AddHttpClient<RaiderIoClient>(c =>
        {
            c.BaseAddress = new Uri("https://raider.io/");
            c.Timeout = TimeSpan.FromSeconds(15);
            c.DefaultRequestHeaders.UserAgent.ParseAdd("PulseMetrics-Alt/1.0");
        });
        builder.Services.AddScoped<IAltPulseApi, AltPulseService>();
        builder.Services.AddScoped<LocalHistoryStore>();

        var app = builder.Build();

        app.UseForwardedHeaders();
        app.UseRequestLocalization();

        // Débogage WebAssembly : lancé directement par Visual Studio / VS Code depuis .NET 11 (plus de middleware).
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error", createScopeForErrors: true);
            app.UseHsts();
        }
        app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
        app.UseHttpsRedirection();
        app.UseAntiforgery();

        app.MapGet("/culture/{culture}", (string culture, string? returnUrl, HttpContext http, IConfiguration configuration) =>
        {
            if (!AltLocalization.Cultures.Contains(culture, StringComparer.OrdinalIgnoreCase))
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
                    IsEssential = true,
                    SameSite = SameSiteMode.Lax,
                    Secure = http.Request.IsHttps
                });

            return Results.LocalRedirect(http.Request.PathBase.Add(new PathString(AltLocalization.SafeReturnUrl(returnUrl))));
        });

        var api = app.MapGroup("/api");
        api.MapGet("/season/{region}", async (string region, IAltPulseApi service, CancellationToken ct) =>
            await service.GetSeasonAsync(region, ct) is { } season ? Results.Ok(season) : Results.NotFound());
        api.MapGet("/realms/{region}", async (string region, IAltPulseApi service, CancellationToken ct) =>
            Results.Ok(await service.GetRealmsAsync(region, ct)));
        api.MapGet("/characters/{region}/{realm}/{name}", async (string region, string realm, string name, bool? refresh, IAltPulseApi service, CancellationToken ct) =>
        {
            var result = await service.GetCharacterAsync(region, realm, name, refresh ?? false, ct);
            return result.Character is null ? Results.NotFound(result) : Results.Ok(result);
        });
        api.MapPost("/addon", async (AddonImportRequest request, IAltPulseApi service, CancellationToken ct) =>
        {
            var result = await service.ImportAddonAsync(request.Text, ct);
            return result.Error is null ? Results.Ok(result) : Results.BadRequest(result);
        });

        app.MapStaticAssets();
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode()
            .AddInteractiveWebAssemblyRenderMode()
            .AddAdditionalAssemblies(typeof(PulseMetrics.Alt.Client._Imports).Assembly);

        return app;
    }
}
