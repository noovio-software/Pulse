using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Noovio.Pulse.Usage;

public static class UsageDependencyInjection
{
    /// <summary>Nom de la chaîne de connexion de la base d'usage, la même dans les trois sites et dans PulseAdmin.</summary>
    public const string ConnectionStringName = "PulseUsage";

    /// <summary>
    /// Côté site : <see cref="IUsageRecorder"/> et l'écriture en tâche de fond. Sans chaîne <c>PulseUsage</c>,
    /// l'enregistreur ne fait rien : le site tourne sans la base d'usage.
    /// </summary>
    public static IServiceCollection AddPulseUsageRecording(this IServiceCollection services, IConfiguration configuration)
    {
        if (configuration.GetConnectionString(ConnectionStringName) is not { Length: > 0 })
        {
            services.AddSingleton<IUsageRecorder, NullUsageRecorder>();
            return services;
        }

        services.AddPulseUsageDatabase(configuration);
        services.AddSingleton<ChannelUsageRecorder>();
        services.AddSingleton<IUsageRecorder>(sp => sp.GetRequiredService<ChannelUsageRecorder>());
        services.AddHostedService<UsageWriter>();
        return services;
    }

    /// <summary>Le contexte de la base d'usage seul (PulseAdmin).</summary>
    public static IServiceCollection AddPulseUsageDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<UsageDbContext>(o => o.UseNpgsql(configuration.GetConnectionString(ConnectionStringName)));
        return services;
    }

    /// <summary>
    /// Applique les migrations en attente. Les trois sites et PulseAdmin le font au démarrage : un échec dû à une migration
    /// appliquée au même instant par un autre processus est attendu, on patiente puis on revérifie.
    /// </summary>
    public static async Task MigrateUsageDatabaseAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        const int attempts = 4;
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<UsageDbContext>().Database;
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Noovio.Pulse.Usage.Migrations");

        for (var attempt = 1; ; attempt++)
        {
            if (!(await db.GetPendingMigrationsAsync(ct)).Any())
            {
                return;
            }

            try
            {
                await db.MigrateAsync(ct);
                logger.LogInformation("Base d'usage migrée");
                return;
            }
            catch (System.Data.Common.DbException ex) when (attempt < attempts)
            {
                logger.LogWarning(ex, "Migration de la base d'usage en échec (essai {Attempt}/{Attempts}) : nouvelle vérification dans 5 s", attempt, attempts);
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
            }
        }
    }
}
