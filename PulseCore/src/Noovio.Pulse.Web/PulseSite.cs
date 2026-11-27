using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Hosting;
using Noovio.Pulse.Application;
using Noovio.Pulse.Application.Abstractions;
using Noovio.Pulse.Domain.Roles;
using Noovio.Pulse.Infrastructure;
using Noovio.Pulse.Infrastructure.Hosting;
using Noovio.Pulse.Infrastructure.Persistence;
using Noovio.Pulse.Usage;
using Noovio.Pulse.Web.Auth;
using Noovio.Pulse.Web.Components;
using Noovio.Pulse.Web.Localization;
using Noovio.Pulse.Web.ShareCard;

namespace Noovio.Pulse.Web;

/// <summary>
/// Site d'une application Pulse (Front, Life, Burst), avec ses workers d'ingestion : l'hôte PulseMetrics fournit le
/// constructeur (déjà relié à son serveur) et monte l'application sous son préfixe (<see cref="PulseApp.Slug"/>).
/// </summary>
public static class PulseSite
{
    /// <param name="workers">Ingestion et synchronisation des routes en tâche de fond, dans le même conteneur que le site.</param>
    public static WebApplication Build<TContext>(WebApplicationBuilder builder, PulseApp pulse, bool workers)
        where TContext : PulseDbContext
    {
        // Hors requête (démarrage, tâches de fond), le français ; chaque requête prend ensuite la langue du visiteur.
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo(PulseLocalization.DefaultCulture);

        builder.Services.AddPulseLocalization();
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();
        // Accents et symboles écrits tels quels (« Conformité ») plutôt qu'en entités (« Conformit&#xE9; ») : les aperçus
        // de lien générés par les applications (Messenger, WhatsApp) lisent les balises avec des analyseurs sommaires.
        // < > & " ' restent échappés.
        builder.Services.AddSingleton(HtmlEncoder.Create(UnicodeRanges.All));

        // Comme AltPulse : Nginx termine le HTTPS et relaie en HTTP sur 127.0.0.1.
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);

        // Le site lit les snapshots (écrits par les workers), gère les comptes et les analyses.
        builder.Services.AddPulseApplication(builder.Configuration, pulse);
        builder.Services.AddPulseInfrastructure<TContext>(builder.Configuration, pulse);
        if (workers)
        {
            builder.Services.AddPulseWorkers(pulse);
        }
        builder.Services.AddBattleNetAuthentication(builder.Configuration, pulse);
        builder.Services.AddPulseAdmin(builder.Configuration);
        // Chaque bench est écrit dans la base d'usage commune, lue par PulseAdmin (sans chaîne PulseUsage : rien n'est écrit).
        builder.Services.AddPulseUsageRecording(builder.Configuration);
        builder.Services.AddSingleton<RefreshSchedule>();
        builder.Services.AddHttpClient(ShareCardRenderer.HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(5));
        builder.Services.AddSingleton<ShareCardRenderer>();
        builder.Services.AddSingleton<ShareCards>();

        // Clés de chiffrement du cookie de connexion : en prod, dans un dossier persistant (sinon chaque redémarrage,
        // donc chaque déploiement, déconnecterait tout le monde). En local, l'emplacement par défaut suffit.
        if (builder.Configuration["DataProtection:KeysPath"] is { Length: > 0 } keysPath)
        {
            builder.Services.AddDataProtection()
                .SetApplicationName(pulse.Name)
                .PersistKeysToFileSystem(new DirectoryInfo(keysPath));
        }

        var app = builder.Build();
        PulseWorker.LogConfigurationWarnings(app.Services, pulse);

        app.UseForwardedHeaders();
        // Avant tout le reste : les pages, l'API et le circuit Blazor (fixé à sa connexion) prennent la langue du visiteur.
        app.UseRequestLocalization();

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error", createScopeForErrors: true);
            app.UseHsts();
        }
        // Page « introuvable » pour les pages seulement : sur /api, un 401 ou un 404 doit rester tel quel
        // (rejoué en POST vers /not-found, il deviendrait un 400 d'antiforgery).
        app.UseWhen(
            context => !context.Request.Path.StartsWithSegments("/api"),
            pages => pages.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true));
        app.UseHttpsRedirection();

        // HEAD servi comme GET : les pages Blazor ne répondent qu'à GET et POST, et un robot d'aperçu (l'application
        // Messenger…) qui sonde un lien en HEAD le croyait mort. Seul le texte de la méthode change : Kestrel garde la
        // requête pour un HEAD et n'envoie que les en-têtes, ceux de la vraie page. Avant le routage, pour qu'il trouve la page.
        app.Use((context, next) =>
        {
            if (HttpMethods.IsHead(context.Request.Method))
            {
                context.Request.Method = HttpMethods.Get;
            }
            return next(context);
        });
        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();
        app.UseAntiforgery();

        app.MapCultureEndpoint();
        app.MapAccountEndpoints();
        app.MapAdminEndpoints();

        var api = app.MapGroup("/api");

        api.MapGet("/specs", async (ISpecMetaQueries queries, CancellationToken ct) =>
            Results.Ok(await queries.GetSummariesAsync(ct)));

        api.MapGet("/specs/{slug}", async (string slug, ISpecMetaQueries queries, CancellationToken ct) =>
            pulse.Role.FromSlug(slug) is { } info && await queries.GetLatestAsync(info.Spec, ct) is { } meta
                ? Results.Ok(meta)
                : Results.NotFound());

        // Vignette d'un lien partagé, lue par Discord, Messenger & co. L'analyse partagée ne change jamais et la langue est
        // dans l'adresse : l'image non plus, d'où un cache long côté client.
        app.MapGet("/partage/{token}/vignette-{language}.jpg",
            async (string token, string language, ShareCards cards, HttpContext http, CancellationToken ct) =>
            {
                if (await cards.GetAsync(token, language, ct) is not { } card)
                {
                    return Results.NotFound();
                }

                http.Response.Headers.CacheControl = "public, max-age=604800, immutable";
                return Results.File(card, ShareCardRenderer.ContentType);
            });

        app.MapStaticAssets();
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();

        return app;
    }
}
