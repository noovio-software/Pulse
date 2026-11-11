using Noovio.BurstPulse.Infrastructure;
using Noovio.BurstPulse.Infrastructure.Persistence;
using Noovio.Pulse.Infrastructure.Hosting;

await PulseWorker.RunAsync<BurstPulseDbContext>(args, BurstPulseApp.Definition);
