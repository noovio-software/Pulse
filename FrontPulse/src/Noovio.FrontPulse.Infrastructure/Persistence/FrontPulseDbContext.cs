using Microsoft.EntityFrameworkCore;
using Noovio.Pulse.Infrastructure.Persistence;

namespace Noovio.FrontPulse.Infrastructure.Persistence;

public sealed class FrontPulseDbContext(DbContextOptions<FrontPulseDbContext> options) : PulseDbContext(options)
{
    /// <summary>Le nom d'avant le partage du moteur : la table existe déjà en production.</summary>
    protected override string ProfilesTable => "tank_profiles";
}
