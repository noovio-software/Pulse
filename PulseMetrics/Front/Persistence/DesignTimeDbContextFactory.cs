using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PulseMetrics.Front.Persistence;

/// <summary>
/// Utilisée seulement par <c>dotnet ef</c> (génération de migrations) : la chaîne n'est jamais ouverte,
/// elle ne sert qu'à choisir le fournisseur PostgreSQL. Évite de dépendre d'un projet de démarrage.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<FrontPulseDbContext>
{
    public FrontPulseDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<FrontPulseDbContext>().UseNpgsql("Host=localhost;Port=5432;Database=pulsemetrics;Username=pulsemetrics;Password=pulsemetrics;Search Path=front,public").Options);
}
