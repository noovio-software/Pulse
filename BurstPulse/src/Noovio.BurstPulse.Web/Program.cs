using Noovio.BurstPulse.Infrastructure;
using Noovio.BurstPulse.Infrastructure.Persistence;
using Noovio.Pulse.Web;

await PulseSite.RunAsync<BurstPulseDbContext>(args, BurstPulseApp.Definition);
