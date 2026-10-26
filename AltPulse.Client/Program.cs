using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using AltPulse.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<IAltPulseApi, HttpAltPulseApi>();
builder.Services.AddScoped<LocalHistoryStore>();

await builder.Build().RunAsync();
