using Microsoft.EntityFrameworkCore;
using Noovio.Pulse.Infrastructure.Persistence;

namespace PulseMetrics.Burst.Persistence;

public sealed class BurstPulseDbContext(DbContextOptions<BurstPulseDbContext> options) : PulseDbContext(options);
