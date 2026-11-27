using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Noovio.Pulse.Usage;

namespace PulseAdmin.Notifications;

/// <summary>Un appareil (navigateur, application installée) abonné aux récaps d'usage.</summary>
public sealed class PushDevice
{
    public long Id { get; set; }

    /// <summary>L'adresse du service de push du navigateur : elle identifie l'appareil.</summary>
    public string Endpoint { get; set; } = "";
    public string P256dh { get; set; } = "";
    public string Auth { get; set; } = "";

    /// <summary>BattleTag de l'admin qui s'est abonné (« local » en développement sans connexion).</summary>
    public string Owner { get; set; } = "";
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>Fin de la dernière période dont le récap est parti vers cet appareil : un redémarrage ne le renvoie pas.</summary>
    public DateTimeOffset? LastDigestUtc { get; set; }
}

/// <summary>
/// Les tables propres à PulseAdmin, dans la même base que les benchs (schéma <c>usage</c>) mais avec leur propre
/// historique de migrations : celles de <see cref="UsageDbContext"/> restent communes aux sites.
/// </summary>
public sealed class AdminDbContext(DbContextOptions<AdminDbContext> options) : DbContext(options)
{
    public const string MigrationsHistoryTable = "__admin_migrations_history";

    public DbSet<PushDevice> PushDevices => Set<PushDevice>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PushDevice>(e =>
        {
            e.ToTable("admin_push_devices");
            e.HasIndex(d => d.Endpoint).IsUnique();
            e.Property(d => d.Endpoint).HasMaxLength(1024);
            e.Property(d => d.P256dh).HasMaxLength(256);
            e.Property(d => d.Auth).HasMaxLength(128);
            e.Property(d => d.Owner).HasMaxLength(64);
        });
    }
}

public static class AdminDatabase
{
    public static IServiceCollection AddAdminDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AdminDbContext>(o => o.UseNpgsql(
            configuration.GetConnectionString(UsageDependencyInjection.ConnectionStringName),
            npgsql => npgsql.MigrationsHistoryTable(AdminDbContext.MigrationsHistoryTable)));
        return services;
    }

    /// <summary>Après <see cref="UsageDependencyInjection.MigrateUsageDatabaseAsync"/>, qui crée le schéma.</summary>
    public static async Task MigrateAdminDatabaseAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AdminDbContext>().Database.MigrateAsync(ct);
    }
}

/// <summary>Utilisée seulement par <c>dotnet ef</c> : la chaîne n'est jamais ouverte, elle choisit le fournisseur.</summary>
public sealed class DesignTimeAdminDbContextFactory : IDesignTimeDbContextFactory<AdminDbContext>
{
    public AdminDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<AdminDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=pulsemetrics;Username=pulsemetrics;Password=pulsemetrics;Search Path=usage,public",
                npgsql => npgsql.MigrationsHistoryTable(AdminDbContext.MigrationsHistoryTable))
            .Options);
}
