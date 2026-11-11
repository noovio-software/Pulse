using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Noovio.BurstPulse.Infrastructure.Persistence;

/// <summary>
/// Utilisée seulement par <c>dotnet ef</c> (génération de migrations) : la chaîne n'est jamais ouverte,
/// elle ne sert qu'à choisir le fournisseur PostgreSQL. Évite de dépendre d'un projet de démarrage.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<BurstPulseDbContext>
{
    public BurstPulseDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<BurstPulseDbContext>().UseNpgsql("Host=localhost;Database=burstpulse").Options);
}
