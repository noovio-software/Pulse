# PulseAdmin by Noovio

Private usage statistics for [FrontPulse](../FrontPulse/README.md), [LifePulse](../LifePulse/README.md) and [BurstPulse](../BurstPulse/README.md): who benches which characters, when, and what the analyses find.
Served at https://pulseadmin.noovio.fr, behind Battle.net sign-in restricted to a list of BattleTags. It is deliberately not linked from the Noovio page, and every response carries `noindex` (header, meta tag, `robots.txt`).

## How the data gets here

```
FrontPulse ─┐
LifePulse  ─┼─ CharacterReport → IUsageRecorder → background writer → PostgreSQL "pulseusage" ← PulseAdmin
BurstPulse ─┘                    (Noovio.Pulse.Usage, in PulseCore)        table bench_events
```

- Every time the analysis page loads a character (including cache hits and the "Actualiser" button), the site writes one `BenchEvent`: app, Battle.net account id, character, spec, outcome, duration, and when the analysis succeeded its numbers (item level and gap to the top, secondary stats and the top's medians, gear verdicts, enchants, sockets, embellishments, talent similarity, hero tree, M+ score, verdict per area, item level per slot).
- Recording never slows down or breaks an analysis: events go to a bounded in-memory queue and are written in batches; if the database is down they are logged and dropped.
- Without a `PulseUsage` connection string, a site records nothing.
- The sites and PulseAdmin all apply the `pulseusage` migrations at startup (`Noovio.Pulse.Usage/Migrations`).

## What it shows

Filters (period, dates, app, class, spec, region, outcome) live in the URL, so a view can be bookmarked. Averages describe characters, not clicks: by default each character and spec counts once, through its latest successful bench ("Moyennes sur tous les benchs" counts every bench).

- Usage: benches, users (new, returning), characters, success and cache rates, bench duration (mean, p95), benches per day stacked by app, hour-of-day curve per app, weekday × hour heatmap, outcomes, regions, factions.
- Character profile: item level (mean, median, spread, histogram, gap to the top), analysis score, M+ score, secondary stats against the top, gear (pieces at the top's level, popularity of the pieces, enchant and socket coverage, talents, hero tree), verdicts per area, item level per slot, classes, specs (full table), races, realms, hero trees.
- Most active accounts, latest benches, a paged list of every bench with search, a detail page per bench, and a CSV export of the filtered benches.

## Run it

```powershell
# PostgreSQL for the usage database, on port 5433 (5432 is the sites' database)
docker compose -f PulseAdmin/docker-compose.yml up -d

dotnet run --project PulseAdmin/PulseAdmin --launch-profile http
# → http://localhost:5190, open without sign-in in Development (Admin:AllowAnonymousInDevelopment)
```

The sites' `appsettings.Development.json` point at the same database: run one of them, bench a character, and it shows up.

## Deploy

Port 8560 behind Nginx, systemd unit `kestrel-pulseadmin`.

1. Point the DNS record `pulseadmin.noovio.fr` at the server.
2. Run `PulseAdmin/deploy/setup-server.sh` on the server. It creates the `pulseusage` database and role, writes the connection string into `/etc/pulseadmin/pulseadmin.env` and appends it to the three sites' env files.
3. Fill in the Blizzard client and your BattleTag in `/etc/pulseadmin/pulseadmin.env`, and add `https://pulseadmin.noovio.fr/signin-battlenet` to the Battle.net client.
4. Restart the three sites so they start recording, after deploying a version that includes `Noovio.Pulse.Usage`.
5. Add the GitHub secret `REMOTE_TARGET_PULSEADMIN` (for example `/home/ubuntu/projects/pulseadmin/`), then push a tag `prod-pulseadmin-x.y.z`.
