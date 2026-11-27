# Life · PulseMetrics

Mythic+ meta for World of Warcraft healers: gear, secondary stats, talents, trinkets and embellishments of the top 100 healers of each spec
(Restoration Shaman, Restoration Druid, Preservation, Mistweaver, Holy Paladin, Discipline, Holy Priest), and a comparison of your own character against them.

It does what [Front](front.md) does for tanks, on the same engine: see [PulseCore](../../PulseCore/README.md).

| Code | Role |
|---|---|
| `PulseMetrics/Life` | `LifePulseApp` (name, role, sigil), `LifePulseDbContext`, migrations |
| `PulseMetrics/wwwroot/life` | `theme.css` (shaman blue `#0070DD`), favicon |

The site and its ingestion worker run in the PulseMetrics process, under `/life/`.

## Run it

See [the PulseMetrics README](../README.md).

**Battle.net client**: add these redirect URLs to the client.

| Host | Redirect URL |
|---|---|
| Local | `http://localhost:8580/life/signin-battlenet` |
| Production | `https://pulsemetrics.fr/life/signin-battlenet` |

Front, Life and Burst share one Blizzard client and, in the PulseMetrics process, one rate limiter (90 requests/s, 35,000/hour, just under the 100/s and 36,000/h quotas).

## Deploy

Deployed with the whole PulseMetrics process: see [the PulseMetrics README](../README.md).
