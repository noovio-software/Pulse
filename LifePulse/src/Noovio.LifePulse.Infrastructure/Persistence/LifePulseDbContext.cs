using Microsoft.EntityFrameworkCore;
using Noovio.Pulse.Infrastructure.Persistence;

namespace Noovio.LifePulse.Infrastructure.Persistence;

public sealed class LifePulseDbContext(DbContextOptions<LifePulseDbContext> options) : PulseDbContext(options);
