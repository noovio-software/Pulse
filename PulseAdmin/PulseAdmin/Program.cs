using System.Globalization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Noovio.Pulse.Usage;
using PulseAdmin.Auth;
using PulseAdmin.Components;
using PulseAdmin.Stats;

// Pourcentages et décimales à la française, comme les sites.
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("fr-FR");

var builder = WebApplication.CreateBuilder(args);

// Rendu statique côté serveur : les filtres sont des formulaires GET, chaque vue a son adresse.
builder.Services.AddRazorComponents();

// Comme les autres sites : Nginx termine le HTTPS et relaie en HTTP sur 127.0.0.1.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);

builder.Services.AddPulseUsageDatabase(builder.Configuration);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<UsageQueries>();
builder.Services.AddAdminAuthentication(builder.Configuration, builder.Environment);

if (builder.Configuration["DataProtection:KeysPath"] is { Length: > 0 } keysPath)
{
    builder.Services.AddDataProtection()
        .SetApplicationName("PulseAdmin")
        .PersistKeysToFileSystem(new DirectoryInfo(keysPath));
}

var app = builder.Build();

// Les sites appliquent aussi les migrations de la base d'usage : PulseAdmin peut démarrer avant ou après eux.
await app.Services.MigrateUsageDatabaseAsync();

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

// Réservé : jamais indexé, même si l'adresse fuit.
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Robots-Tag"] = "noindex, nofollow, noarchive";
    await next();
});

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapAdminAccountEndpoints();

app.MapGet("/export.csv", async (HttpContext context, UsageQueries queries, CancellationToken ct) =>
    Results.File(await queries.ExportCsvAsync(StatsFilter.Parse(context.Request.Query), ct), "text/csv; charset=utf-8",
        $"pulse-benchs-{DateTime.UtcNow:yyyyMMdd-HHmm}.csv"))
    .RequireAuthorization(AdminAuthentication.Policy);

app.MapStaticAssets().AllowAnonymous();
app.MapRazorComponents<App>();

app.Run();
