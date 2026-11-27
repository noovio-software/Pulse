# Burst · PulseMetrics

Mythic+ meta for World of Warcraft damage dealers: gear, secondary stats, talents, trinkets and embellishments of the top 100 DPS of each of the 27 damage specs
(every class, Devourer included), and a comparison of your own character against them.

It does what [Front](front.md) does for tanks and [Life](life.md) for healers, on the same engine: see [PulseCore](../../PulseCore/README.md).

| Code | Role |
|---|---|
| `PulseMetrics/Burst` | `BurstPulseApp` (name, role, sigil), `BurstPulseDbContext`, migrations |
| `PulseMetrics/wwwroot/burst` | `theme.css` (Midnight violet `#7C3AED`, fuchsia second), favicon |

The site and its ingestion worker run in the PulseMetrics process, under `/burst/`.

## Run it

See [the PulseMetrics README](../README.md).

**Battle.net client**: add these redirect URLs to the client.

| Host | Redirect URL |
|---|---|
| Local | `http://localhost:8580/burst/signin-battlenet` |
| Production | `https://pulsemetrics.fr/burst/signin-battlenet` |

Front, Life and Burst share one Blizzard client and, in the PulseMetrics process, one rate limiter (90 requests/s, 35,000/hour, just under the 100/s and 36,000/h quotas). Burst is the heaviest of the three: 27 specs × 130 candidates × 3 profile calls is about 10,500 requests per ingestion.

## Deploy

Deployed with the whole PulseMetrics process: see [the PulseMetrics README](../README.md).
