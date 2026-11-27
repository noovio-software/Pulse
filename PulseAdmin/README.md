# PulseMetrics Admin

Private usage statistics for [Front](../PulseMetrics/docs/front.md), [Life](../PulseMetrics/docs/life.md) and [Burst](../PulseMetrics/docs/burst.md): who benches which characters, when, and what the analyses find.
Served only at https://admin.pulsemetrics.fr, behind Battle.net sign-in restricted to a list of BattleTags. It is deliberately not linked from the public page, and every response carries `noindex` (header, meta tag, `robots.txt`).

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

- Today, at the top and regardless of the filters: benches since midnight (Paris) against yesterday at the same time, users, and the name of each user once (latest character benched; "addon" when imported without signing in), linking to their benches.
- Usage: benches, users (new, returning), characters, success and cache rates, bench duration (mean, p95), benches per day stacked by app, hour-of-day curve per app, weekday × hour heatmap, outcomes, regions, factions.
- Character profile: item level (mean, median, spread, histogram, gap to the top), analysis score, M+ score, secondary stats against the top, gear (pieces at the top's level, popularity of the pieces, enchant and socket coverage, talents, hero tree), verdicts per area, item level per slot, classes, specs (full table), races, realms, hero trees.
- Most active accounts, latest benches, a paged list of every bench with search, a detail page per bench, and a CSV export of the filtered benches.

## Application and notifications

- **Install**: in Chrome or Edge, "Installer l'app" in the top bar (or the install icon of the address bar) opens the admin in its own window, with its icon in the Start menu and taskbar. A web manifest and a service worker make it installable; the service worker caches nothing, pages stay private and fresh.
- **Notifications**: "Activer les notifications" subscribes the device (Web Push, VAPID, like Nowtilus). At 7:00, 12:00, 18:00 and 21:00 (Paris, `Notifications:Times`), each subscribed device receives the benches since the previous recap: count, users, names. "Tester" sends today's figures to the current device. A device remembers the last recap it received, so a restart sends nothing twice; a recap missed for more than an hour is skipped. Devices are stored in `admin_push_devices`, with their own migrations history (`__admin_migrations_history`).
- **Discord**: NoovioBot posts the same recap in the private admin channel and answers `!stats X` (last X hours), through `GET /api/usage/summary?hours=X` or `?from=…&to=…` (ISO 8601), authenticated by `Authorization: Bearer <Api:Key>`. Without `Api:Key` the API answers 401.

Without `Notifications:PublicKey`/`PrivateKey`, the buttons are hidden and no recap is sent. `PulseAdmin --vapid-keys` prints a new pair.

## Run it

```powershell
# PostgreSQL for the usage database, on port 5433 (5432 is the sites' database)
docker compose -f PulseMetrics/docker-compose.yml up -d

dotnet run --project PulseAdmin/PulseAdmin --launch-profile PulseMetrics
# → http://localhost:5190, open without sign-in in Development (Admin:AllowAnonymousInDevelopment)
```

The sites' `appsettings.Development.json` point at the same database: run one of them, bench a character, and it shows up.

## Deploy

Port 8560 behind Nginx, systemd unit `kestrel-pulseadmin`.

1. Point the DNS record `admin.pulsemetrics.fr` at the server.
2. Run `PulseAdmin/deploy/setup-server.sh` on the server. It copies the shared `ConnectionStrings__PulseUsage` from `/etc/pulsemetrics/pulsemetrics.env`; the admin tables use the `usage` schema of the same `pulsemetrics` database.
3. Fill in the Blizzard client and your BattleTag in `/etc/pulseadmin/pulseadmin.env`, and add `https://admin.pulsemetrics.fr/signin-battlenet` to the Battle.net client.
4. Restart the three sites so they start recording, after deploying a version that includes `Noovio.Pulse.Usage`.
5. Add the GitHub secret `REMOTE_TARGET_PULSEADMIN` (for example `/home/ubuntu/projects/pulseadmin/`), then push a tag `prod-admin-x.y.z`.
6. Run `setup-server.sh` again after the first deployment: it generates `Api__Key` and, now that the executable exists, the VAPID keys (`Notifications__PublicKey`/`PrivateKey`). Existing values are kept. Copy `Api__Key` into `PULSEADMIN_API_KEY` of NoovioBot, then restart both services.
