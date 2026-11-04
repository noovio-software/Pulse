using Noovio.LifePulse.Infrastructure;
using Noovio.LifePulse.Infrastructure.Persistence;
using Noovio.Pulse.Infrastructure.Hosting;

await PulseWorker.RunAsync<LifePulseDbContext>(args, LifePulseApp.Definition);
