using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Noovio.Pulse.Usage;

/// <summary>La base commune des statistiques d'usage (<c>pulseusage</c>) : écrite par les trois sites, lue par PulseAdmin.</summary>
public sealed class UsageDbContext(DbContextOptions<UsageDbContext> options) : DbContext(options)
{
    public DbSet<BenchEvent> BenchEvents => Set<BenchEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BenchEvent>(e =>
        {
            e.ToTable("bench_events");
            e.HasIndex(b => b.OccurredAtUtc);
            e.HasIndex(b => new { b.App, b.OccurredAtUtc });
            e.HasIndex(b => b.BattleNetId);
            e.Property(b => b.App).HasMaxLength(32);
            e.Property(b => b.Role).HasMaxLength(16);
            e.Property(b => b.Status).HasMaxLength(24);
            e.Property(b => b.Region).HasMaxLength(4);
            e.Property(b => b.RealmSlug).HasMaxLength(64);
            e.Property(b => b.RealmName).HasMaxLength(64);
            e.Property(b => b.CharacterName).HasMaxLength(64);
            e.Property(b => b.ClassName).HasMaxLength(64);
            e.Property(b => b.Race).HasMaxLength(64);
            e.Property(b => b.Faction).HasMaxLength(16);
            e.Property(b => b.SpecSlug).HasMaxLength(64);
            e.Property(b => b.SpecName).HasMaxLength(64);
            e.Property(b => b.HeroTree).HasMaxLength(64);
            e.Property(b => b.TopHeroTree).HasMaxLength(64);
            e.Property(b => b.Error).HasMaxLength(256);
            e.Property(b => b.Areas).HasJson();
            e.Property(b => b.SlotItemLevels).HasJson();
        });
    }
}

internal static class UsageJson
{
    public static PropertyBuilder<T?> HasJson<T>(this PropertyBuilder<T?> property) where T : class
    {
        property.HasConversion(
            v => v == null ? null : JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
            v => v == null ? null : JsonSerializer.Deserialize<T>(v, JsonSerializerOptions.Default),
            new ValueComparer<T?>(
                (a, b) => JsonSerializer.Serialize(a, JsonSerializerOptions.Default) == JsonSerializer.Serialize(b, JsonSerializerOptions.Default),
                v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default).GetHashCode(),
                v => v == null ? null : JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(v, JsonSerializerOptions.Default), JsonSerializerOptions.Default)));
        property.HasColumnType("jsonb");
        return property;
    }
}

/// <summary>Utilisée seulement par <c>dotnet ef</c> : la chaîne n'est jamais ouverte, elle choisit le fournisseur.</summary>
public sealed class DesignTimeUsageDbContextFactory : IDesignTimeDbContextFactory<UsageDbContext>
{
    public UsageDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<UsageDbContext>().UseNpgsql("Host=localhost;Database=pulseusage").Options);
}
