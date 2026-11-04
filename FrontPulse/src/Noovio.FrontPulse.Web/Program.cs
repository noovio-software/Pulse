using Noovio.FrontPulse.Infrastructure;
using Noovio.FrontPulse.Infrastructure.Persistence;
using Noovio.Pulse.Web;

await PulseSite.RunAsync<FrontPulseDbContext>(args, FrontPulseApp.Definition);
