using Noovio.LifePulse.Infrastructure;
using Noovio.LifePulse.Infrastructure.Persistence;
using Noovio.Pulse.Web;

await PulseSite.RunAsync<LifePulseDbContext>(args, LifePulseApp.Definition);
