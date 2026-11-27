using Microsoft.EntityFrameworkCore;
using Noovio.Pulse.Infrastructure.Persistence;

namespace PulseMetrics.Life.Persistence;

public sealed class LifePulseDbContext(DbContextOptions<LifePulseDbContext> options) : PulseDbContext(options);
