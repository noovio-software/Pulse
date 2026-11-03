using System.Globalization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Noovio.FrontPulse.Application;
using Noovio.FrontPulse.Application.Abstractions;
using Noovio.FrontPulse.Web.Auth;
using Noovio.FrontPulse.Domain;
using Noovio.FrontPulse.Infrastructure;
using Noovio.FrontPulse.Web.Components;

// Pourcentages et décimales à la française (« 24,3 % ») dans tous les circuits.
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("fr-FR");

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Comme AltPulse : Nginx termine le HTTPS et relaie en HTTP sur 127.0.0.1.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);

// Le site lit les snapshots (l'ingestion tourne dans Noovio.FrontPulse.Worker), gère les comptes et les analyses.
builder.Services.AddFrontPulseApplication(builder.Configuration);
builder.Services.AddFrontPulseInfrastructure(builder.Configuration);
builder.Services.AddBattleNetAuthentication(builder.Configuration);

// Clés de chiffrement du cookie de connexion : en prod, dans un dossier persistant (sinon chaque redémarrage,
// donc chaque déploiement, déconnecterait tout le monde). En local, l'emplacement par défaut suffit.
if (builder.Configuration["DataProtection:KeysPath"] is { Length: > 0 } keysPath)
{
    builder.Services.AddDataProtection()
        .SetApplicationName("FrontPulse")
        .PersistKeysToFileSystem(new DirectoryInfo(keysPath));
}

var app = builder.Build();

// Le worker applique aussi les migrations au démarrage : EF Core sérialise les deux par un verrou en base.
await app.Services.MigrateFrontPulseDatabaseAsync();

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapAccountEndpoints();

var api = app.MapGroup("/api");

api.MapGet("/specs", async (ISpecMetaQueries queries, CancellationToken ct) =>
    Results.Ok(await queries.GetSummariesAsync(ct)));

api.MapGet("/specs/{slug}", async (string slug, ISpecMetaQueries queries, CancellationToken ct) =>
    TankSpecs.FromSlug(slug) is { } info && await queries.GetLatestAsync(info.Spec, ct) is { } meta
        ? Results.Ok(meta)
        : Results.NotFound());

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
