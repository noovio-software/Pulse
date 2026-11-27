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
var culture = new CultureInfo(AltLocalization.Resolve(await host.Services.GetRequiredService<IJSRuntime>().InvokeAsync<string>("altpulse.culture")));
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = culture;

await host.RunAsync();
