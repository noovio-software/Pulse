using Microsoft.EntityFrameworkCore;
using Noovio.Pulse.Infrastructure.Persistence;

namespace Noovio.BurstPulse.Infrastructure.Persistence;

public sealed class BurstPulseDbContext(DbContextOptions<BurstPulseDbContext> options) : PulseDbContext(options);
