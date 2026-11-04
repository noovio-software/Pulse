using Noovio.FrontPulse.Infrastructure;
using Noovio.FrontPulse.Infrastructure.Persistence;
using Noovio.Pulse.Infrastructure.Hosting;

await PulseWorker.RunAsync<FrontPulseDbContext>(args, FrontPulseApp.Definition);
