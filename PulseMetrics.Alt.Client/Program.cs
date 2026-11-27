using System.Globalization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;
using PulseMetrics.Alt.Client.Localization;
using PulseMetrics.Alt.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<IAltPulseApi, HttpAltPulseApi>();
builder.Services.AddScoped<LocalHistoryStore>();

var host = builder.Build();

// Même langue que le prérendu serveur : celle écrite dans <html lang>. Fixée avant RunAsync, qui charge alors ses textes.
string? language;
try
{
    language = await host.Services.GetRequiredService<IJSRuntime>().InvokeAsync<string>("altpulse.culture");
}
catch (JSException)
{
    // app.js absent ou en erreur : la langue par défaut.
    language = null;
}
var culture = new CultureInfo(AltLocalization.Resolve(language));
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = culture;

await host.RunAsync();
