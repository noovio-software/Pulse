using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Noovio.FrontPulse.Domain.Entities;

namespace Noovio.FrontPulse.Infrastructure.Persistence;

public sealed class FrontPulseDbContext(DbContextOptions<FrontPulseDbContext> options) : DbContext(options)
{
    public DbSet<TankProfile> TankProfiles => Set<TankProfile>();
    public DbSet<SpecMeta> SpecMetas => Set<SpecMeta>();
    public DbSet<DungeonRoute> DungeonRoutes => Set<DungeonRoute>();
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();
    public DbSet<UserCharacter> UserCharacters => Set<UserCharacter>();
    public DbSet<IngestionRequest> IngestionRequests => Set<IngestionRequest>();
    public DbSet<IngestionRun> IngestionRuns => Set<IngestionRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TankProfile>(e =>
        {
            e.ToTable("tank_profiles");
            e.HasIndex(p => new { p.Season, p.Spec, p.Rank });
            e.Property(p => p.Season).HasMaxLength(64);
            e.Property(p => p.Region).HasMaxLength(4);
            e.Property(p => p.RealmSlug).HasMaxLength(64);
            e.Property(p => p.Name).HasMaxLength(64);
            e.Property(p => p.Gear).HasJsonConversion();
            e.Property(p => p.Stats).HasJsonConversion();
            e.Property(p => p.Talents).HasJsonConversion();
        });

        modelBuilder.Entity<SpecMeta>(e =>
        {
            e.ToTable("spec_metas");
            e.HasIndex(m => new { m.Season, m.Spec }).IsUnique();
            e.Property(m => m.Season).HasMaxLength(64);
            e.Property(m => m.Snapshot).HasJsonConversion();
        });

        modelBuilder.Entity<DungeonRoute>(e =>
        {
            e.ToTable("dungeon_routes");
            e.HasIndex(r => new { r.Season, r.DungeonSlug });
            e.Property(r => r.Season).HasMaxLength(64);
            e.Property(r => r.DungeonSlug).HasMaxLength(64);
        });

        modelBuilder.Entity<IngestionRequest>(e =>
        {
            e.ToTable("ingestion_requests");
            e.HasIndex(r => r.Status);
            e.Property(r => r.RequestedBy).HasMaxLength(64);
            e.Property(r => r.Status).HasConversion<string>().HasMaxLength(16);
        });

        modelBuilder.Entity<IngestionRun>(e =>
        {
            e.ToTable("ingestion_runs");
            e.HasIndex(r => r.StartedAtUtc);
            e.Property(r => r.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(r => r.Phase).HasConversion<string>().HasMaxLength(16);
            e.Property(r => r.CurrentSpec).HasConversion<string>().HasMaxLength(32);
        });

        modelBuilder.Entity<UserAccount>(e =>
        {
            e.ToTable("user_accounts");
            e.HasIndex(a => a.BattleNetId).IsUnique();
            e.Property(a => a.BattleTag).HasMaxLength(64);
            e.HasMany(a => a.Characters).WithOne().HasForeignKey(c => c.AccountId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserCharacter>(e =>
        {
            e.ToTable("user_characters");
            e.HasIndex(c => new { c.AccountId, c.Region, c.BlizzardCharacterId }).IsUnique();
            e.Property(c => c.Region).HasMaxLength(4);
            e.Property(c => c.RealmSlug).HasMaxLength(64);
            e.Property(c => c.RealmName).HasMaxLength(64);
            e.Property(c => c.Name).HasMaxLength(64);
            e.Property(c => c.ClassName).HasMaxLength(64);
            e.Property(c => c.RaceName).HasMaxLength(64);
            e.Property(c => c.Faction).HasMaxLength(16);
        });
    }
}

/// <summary>
/// Les agrégats (équipement, talents, snapshot) sont des graphes de records lus d'un bloc :
/// une colonne jsonb chacun, plutôt qu'une dizaine de tables jamais requêtées séparément.
/// </summary>
internal static class JsonColumn
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static PropertyBuilder<T> HasJsonConversion<T>(this PropertyBuilder<T> property)
    {
        property.HasConversion(
            v => JsonSerializer.Serialize(v, Options),
            v => JsonSerializer.Deserialize<T>(v, Options)!,
            new ValueComparer<T>(
                (a, b) => JsonSerializer.Serialize(a, Options) == JsonSerializer.Serialize(b, Options),
                v => JsonSerializer.Serialize(v, Options).GetHashCode(),
                v => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(v, Options), Options)!));
        property.HasColumnType("jsonb");
        return property;
    }
}
