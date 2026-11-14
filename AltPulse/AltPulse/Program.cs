using Microsoft.AspNetCore.HttpOverrides;
using AltPulse.Client.Services;
using AltPulse.Components;
using AltPulse.Services;
using AltPulse.Services.Blizzard;
using AltPulse.Services.RaiderIo;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

// En prod, Nginx termine le HTTPS et relaie en HTTP sur 127.0.0.1 : sans ces en-têtes,
// l'app se croit en HTTP et UseHttpsRedirection boucle. Nginx étant en local, il fait
// partie des proxys connus par défaut (loopback).
builder.Services.Configure<ForwardedHeadersOptions>(options =>
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);

builder.Services.AddMemoryCache();
builder.Services.Configure<BlizzardOptions>(builder.Configuration.GetSection(BlizzardOptions.SectionName));
builder.Services.AddHttpClient<BlizzardClient>(c => c.Timeout = TimeSpan.FromSeconds(15));
builder.Services.AddHttpClient<RaiderIoClient>(c =>
{
    c.BaseAddress = new Uri("https://raider.io/");
    c.Timeout = TimeSpan.FromSeconds(15);
    c.DefaultRequestHeaders.UserAgent.ParseAdd("AltPulse/1.0");
});

// Pendant le prérendu et en mode Interactive Server, les composants appellent directement le service ;
// en WebAssembly, ils passent par HttpAltPulseApi → endpoints ci-dessous.
builder.Services.AddScoped<IAltPulseApi, AltPulseService>();
builder.Services.AddScoped<LocalHistoryStore>();

var app = builder.Build();

app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

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
    .AddAdditionalAssemblies(typeof(AltPulse.Client._Imports).Assembly);

app.Run();
