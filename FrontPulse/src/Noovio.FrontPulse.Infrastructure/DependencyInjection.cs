using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Noovio.FrontPulse.Application.Abstractions;
using Noovio.FrontPulse.Infrastructure.Blizzard;
using Noovio.FrontPulse.Infrastructure.Http;
using Noovio.FrontPulse.Infrastructure.Persistence;
using Noovio.FrontPulse.Infrastructure.RaiderIo;

namespace Noovio.FrontPulse.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddFrontPulseInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<BlizzardOptions>(configuration.GetSection(BlizzardOptions.SectionName));
        services.Configure<RaiderIoOptions>(configuration.GetSection(RaiderIoOptions.SectionName));

        services.AddDbContext<FrontPulseDbContext>(o => o.UseNpgsql(configuration.GetConnectionString("FrontPulse")));

        services.TryAddSingleton(TimeProvider.System);
        services.AddMemoryCache();
        if (configuration.GetConnectionString("Redis") is { Length: > 0 } redis)
        {
            services.AddStackExchangeRedisCache(o =>
            {
                o.Configuration = redis;
                o.InstanceName = "frontpulse:";
            });
        }
        else
        {
            // Sans Redis (développement), le cache reste local au processus.
            services.AddDistributedMemoryCache();
        }

        services.AddSingleton<BlizzardRateLimiter>();
        services.AddSingleton<RaiderIoRateLimiter>();
        services.AddTransient<RateLimitingHandler<BlizzardRateLimiter>>();
        services.AddTransient<RateLimitingHandler<RaiderIoRateLimiter>>();

        services.AddHttpClient<BlizzardOAuthClient>(c => c.Timeout = TimeSpan.FromSeconds(15));
        services.AddHttpClient<BlizzardClient>(c => c.Timeout = TimeSpan.FromSeconds(20))
            .AddHttpMessageHandler<RateLimitingHandler<BlizzardRateLimiter>>();
        services.AddHttpClient<RaiderIoClient>(c =>
            {
                c.BaseAddress = new Uri("https://raider.io/");
                c.Timeout = TimeSpan.FromSeconds(20);
                c.DefaultRequestHeaders.UserAgent.ParseAdd("FrontPulse/1.0");
            })
            .AddHttpMessageHandler<RateLimitingHandler<RaiderIoRateLimiter>>();

        services.AddScoped<ILeaderboardSource, RaiderIoLeaderboardSource>();
        services.AddScoped<BlizzardCharacterSource>();
        services.AddScoped<ICharacterLoadoutSource>(sp => sp.GetRequiredService<BlizzardCharacterSource>());
        services.AddScoped<ICharacterDetailsSource>(sp => sp.GetRequiredService<BlizzardCharacterSource>());
        services.AddScoped<IAccountCharactersSource, BlizzardAccountSource>();
        services.AddScoped<IAccountStore, AccountStore>();
        services.AddScoped<IMediaSource, BlizzardMediaSource>();
        services.AddScoped<ITalentTreeSource, BlizzardTalentTreeSource>();
        services.AddScoped<ITankMetaStore, TankMetaStore>();
        services.AddScoped<ISpecMetaQueries, SpecMetaQueries>();
        return services;
    }

    /// <summary>
    /// Au démarrage du site et du worker : détecte les migrations pas encore jouées et les applique
    /// (la base est créée si elle n'existe pas). Sans effet si la base est à jour.
    /// Le site et le worker redémarrent ensemble à chaque déploiement : si l'autre processus applique
    /// les mêmes migrations au même instant, l'échec (« relation already exists ») est attendu ;
    /// on patiente puis on revérifie, au lieu de faire tomber le service.
    /// </summary>
    public static async Task MigrateFrontPulseDatabaseAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        const int attempts = 4;
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FrontPulseDbContext>().Database;
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Noovio.FrontPulse.Migrations");

        for (var attempt = 1; ; attempt++)
        {
            var pending = (await db.GetPendingMigrationsAsync(ct)).ToList();
            if (pending.Count == 0)
            {
                logger.LogInformation("Base de données à jour");
                return;
            }

            try
            {
                logger.LogInformation("{Count} migration(s) à appliquer : {Migrations}", pending.Count, string.Join(", ", pending));
                await db.MigrateAsync(ct);
                logger.LogInformation("Migrations appliquées");
                return;
            }
            catch (System.Data.Common.DbException ex) when (attempt < attempts)
            {
                logger.LogWarning(ex, "Migration en échec (essai {Attempt}/{Attempts}), sans doute appliquée en parallèle par l'autre service : nouvelle vérification dans 5 s", attempt, attempts);
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
            }
        }
    }
}
