using Microsoft.AspNetCore.HttpOverrides;
using LandingPagePulse.Components;

var builder = WebApplication.CreateBuilder(args);

// Rendu statique côté serveur : la page n'a besoin ni de circuit, ni de WebAssembly.
builder.Services.AddRazorComponents();

// Comme AltPulse : Nginx termine le HTTPS et relaie en HTTP sur 127.0.0.1.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);

var app = builder.Build();

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

// Une redirection par outil (/altpulse, /frontpulse) : les adresses vivent dans la configuration.
// 302 et non 301 : un navigateur ne mémorise pas la cible, qui peut donc changer sans laisser de trace.
foreach (var (slug, url) in app.Configuration.GetRequiredSection("Tools").Get<Dictionary<string, string>>()!)
{
    app.MapGet($"/{slug}", () => Results.Redirect(url));
}

app.MapStaticAssets();
app.MapRazorComponents<App>();

app.Run();
