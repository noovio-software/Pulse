using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Noovio.Pulse.Application.Accounts;
using Noovio.Pulse.Application.Analysis;
using Noovio.Pulse.Application.Ingestion;
using Noovio.Pulse.Application.Meta;
using Noovio.Pulse.Application.Routes;
using Noovio.Pulse.Domain.Roles;

namespace Noovio.Pulse.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddPulseApplication(this IServiceCollection services, IConfiguration configuration, PulseApp app)
    {
        services.AddSingleton(app);
        services.AddSingleton(app.Role);
        services.Configure<MetaOptions>(configuration.GetSection($"{app.Name}:{MetaOptions.SectionName}"));
        services.Configure<IngestionOptions>(configuration.GetSection($"{app.Name}:{IngestionOptions.SectionName}"));

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<MetaAggregatorService>();
        services.AddScoped<MetaIngestionService>();
        services.AddScoped<AccountSyncService>();
        services.AddScoped<CharacterAnalysisService>();
        services.Configure<RouteSyncOptions>(configuration.GetSection($"{app.Name}:{RouteSyncOptions.SectionName}"));
        services.AddScoped<RouteSyncService>();
        return services;
    }
}
