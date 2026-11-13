using System.Globalization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Hosting;
using Noovio.Pulse.Application;
using Noovio.Pulse.Application.Abstractions;
using Noovio.Pulse.Domain.Roles;
using Noovio.Pulse.Infrastructure;
using Noovio.Pulse.Infrastructure.Persistence;
using Noovio.Pulse.Usage;
using Noovio.Pulse.Web.Auth;
using Noovio.Pulse.Web.Components;

namespace Noovio.Pulse.Web;

/// <summary>Hôte du site d'une application Pulse : le <c>Program.cs</c> de l'application se réduit à cet appel.</summary>
public static class PulseSite
{
    public static async Task RunAsync<TContext>(string[] args, PulseApp pulse) where TContext : PulseDbContext
    {
        // Pourcentages et décimales à la française (« 24,3 % ») dans tous les circuits.
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("fr-FR");

        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();

        // Comme AltPulse : Nginx termine le HTTPS et relaie en HTTP sur 127.0.0.1.
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);

        // Le site lit les snapshots (l'ingestion tourne dans le worker), gère les comptes et les analyses.
        builder.Services.AddPulseApplication(builder.Configuration, pulse);
        builder.Services.AddPulseInfrastructure<TContext>(builder.Configuration, pulse);
        builder.Services.AddBattleNetAuthentication(builder.Configuration, pulse);
        builder.Services.AddPulseAdmin(builder.Configuration);
        // Chaque bench est écrit dans la base d'usage commune, lue par PulseAdmin (sans chaîne PulseUsage : rien n'est écrit).
        builder.Services.AddPulseUsageRecording(builder.Configuration);
        builder.Services.AddSingleton<RefreshSchedule>();

        // Clés de chiffrement du cookie de connexion : en prod, dans un dossier persistant (sinon chaque redémarrage,
        // donc chaque déploiement, déconnecterait tout le monde). En local, l'emplacement par défaut suffit.
        if (builder.Configuration["DataProtection:KeysPath"] is { Length: > 0 } keysPath)
        {
            builder.Services.AddDataProtection()
                .SetApplicationName(pulse.Name)
                .PersistKeysToFileSystem(new DirectoryInfo(keysPath));
        }

        var app = builder.Build();

        // Le worker applique aussi les migrations au démarrage : EF Core sérialise les deux par un verrou en base.
        await app.Services.MigratePulseDatabaseAsync();

        app.UseForwardedHeaders();

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

        app.UseAuthentication();
        app.UseAuthorization();
        app.UseAntiforgery();

        app.MapAccountEndpoints();
        app.MapAdminEndpoints();

        var api = app.MapGroup("/api");

        api.MapGet("/specs", async (ISpecMetaQueries queries, CancellationToken ct) =>
            Results.Ok(await queries.GetSummariesAsync(ct)));

        api.MapGet("/specs/{slug}", async (string slug, ISpecMetaQueries queries, CancellationToken ct) =>
            pulse.Role.FromSlug(slug) is { } info && await queries.GetLatestAsync(info.Spec, ct) is { } meta
                ? Results.Ok(meta)
                : Results.NotFound());

        app.MapStaticAssets();
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();

        await app.RunAsync();
    }
}
