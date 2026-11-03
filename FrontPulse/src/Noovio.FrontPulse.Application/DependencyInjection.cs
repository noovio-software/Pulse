using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Noovio.FrontPulse.Application.Accounts;
using Noovio.FrontPulse.Application.Analysis;
using Noovio.FrontPulse.Application.Ingestion;
using Noovio.FrontPulse.Application.Meta;

namespace Noovio.FrontPulse.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddFrontPulseApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MetaOptions>(configuration.GetSection(MetaOptions.SectionName));
        services.Configure<IngestionOptions>(configuration.GetSection(IngestionOptions.SectionName));

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<TankMetaAggregatorService>();
        services.AddScoped<TankMetaIngestionService>();
        services.AddScoped<AccountSyncService>();
        services.AddScoped<CharacterAnalysisService>();
        return services;
    }
}
