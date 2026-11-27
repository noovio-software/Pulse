# PulseMetrics

One executable, one port, one deployment for every public tool:

| Path | Tool | Docs |
|---|---|---|
| `/` | Landing page (French / English) | |
| `/alt/` | Alt, character tracker (Interactive Auto: Server then WebAssembly) | [docs/alt.md](docs/alt.md) |
| `/front/` | Front, Mythic+ tank meta | [docs/front.md](docs/front.md) |
| `/life/` | Life, Mythic+ healer meta | [docs/life.md](docs/life.md) |
| `/burst/` | Burst, Mythic+ DPS meta | [docs/burst.md](docs/burst.md) |

PulseMetrics Admin stays a separate application on its own domain (see [PulseAdmin](../PulseAdmin/README.md)).

## Architecture

```
                     ┌─────────────── PulseMetrics process ───────────────┐
nginx ──► Kestrel ──►│ host: Tools.DispatchAsync (prefix → tool)          │
 :443     :8580      │   /alt/*   ─► Alt     app (own DI, own pipeline)   │
                     │   /front/* ─► Front   app + ingestion & route workers
                     │   /life/*  ─► Life    app + ingestion worker       │
                     │   /burst/* ─► Burst   app + ingestion worker       │
                     │   /*       ─► Landing app                          │
                     └────────────────────────────────────────────────────┘
```

Only the host listens. Each tool is a complete ASP.NET Core application (container, configuration, middlewares,
hosted services) running on an in-process server (`Hosting/EmbeddedServer.cs`): the host hands it the request with
`PathBase` set to its prefix, so pages, `<base href>`, Blazor circuits, WebSockets and static assets all live under
`/front/…` without the tool knowing about the others. Front, Life and Burst share the PulseCore engine but keep their own
`PulseApp`, database and options; their Blizzard and Raider.io rate limiters are shared, since they use one client and one IP.

| Folder | Content |
|---|---|
| `Hosting/` | Single-port host: in-process server, prefix dispatch, start/stop of the tools |
| `Landing/` | Landing page (static server rendering, three explicit endpoints, no Blazor router) |
| `Alt/` | Alt server side: page shell, `/api/*` endpoints, Raider.io and Blizzard clients |
| `Front/`, `Life/`, `Burst/` | `PulseApp` definition, `DbContext`, EF migrations |
| `wwwroot/{landing,alt,front,life,burst}/` | Styles, themes and icons of each tool |
| `../PulseMetrics.Alt.Client/` | Alt's WebAssembly client (a separate project: the WebAssembly SDK requires it) |
| `../PulseCore/` | Engine shared by Front, Life and Burst (pages, rules, API clients, workers) |

The landing page has no `@page` routes: its components live in the same assembly as Alt's, and Blazor discovers every
routable component of an assembly. It is rendered by `RazorComponentResult` on `/`, `/not-found` and `/Error`.

## Run it

```powershell
# PostgreSQL + Redis (or point the connection strings at existing instances): one database, one schema per tool
docker compose -f PulseMetrics/docker-compose.yml up -d

# Blizzard key (API calls and Battle.net sign-in), Keystone.guru account (Front's dungeon routes)
dotnet user-secrets set "Blizzard:ClientId" "<client-id>" --project PulseMetrics
dotnet user-secrets set "Blizzard:ClientSecret" "<client-secret>" --project PulseMetrics
dotnet user-secrets set "KeystoneGuru:Email" "<email>" --project PulseMetrics
dotnet user-secrets set "KeystoneGuru:Password" "<password>" --project PulseMetrics

dotnet run --project PulseMetrics --launch-profile PulseMetrics
# → http://localhost:8580/  /alt/  /front/  /life/  /burst/
```

Command-line switches:

| Switch | Effect |
|---|---|
| `--no-workers` | Sites only, no ingestion nor route sync (launch profile *PulseMetrics (sans workers)*) |
| `--rebuild-snapshots` | Recomputes Front, Life and Burst snapshots from the stored profiles, then exits |
| `--sync-routes` | Syncs Front's dungeon routes now, then exits |

Pending EF migrations of each tool are applied at startup, before the port listens. A connection string with
`Search Path=front,public` puts the tool in its own schema (created if missing); without it, the tool uses its own database.

Battle.net redirect URLs to declare on https://develop.battle.net/access/clients:
`http://localhost:8580/{front,life,burst}/signin-battlenet` and `https://pulsemetrics.fr/{front,life,burst}/signin-battlenet`.

Migrations: `dotnet ef migrations add <Name> --project PulseMetrics --context FrontPulseDbContext --output-dir Front/Persistence/Migrations`
(same for `LifePulseDbContext` / `BurstPulseDbContext`).

## Production (https://pulsemetrics.fr)

Hosted on the OVH VPS (user `ubuntu`), behind Nginx.

| Piece | Location |
|---|---|
| App files | `/home/ubuntu/projects/pulsemetrics/` |
| systemd unit | `/etc/systemd/system/kestrel-pulsemetrics.service` (Kestrel on `127.0.0.1:8580`) |
| Secrets | `/etc/pulsemetrics/pulsemetrics.env`, `root:root 600`, loaded by `EnvironmentFile=` |
| Data protection keys | `/var/lib/pulsemetrics/keys` |
| Nginx vhosts | `/etc/nginx/sites-available/pulsemetrics`, `pulsemetrics-legacy` (old domains → redirects) |

### First install / migration from the per-tool services

1. `scp -r PulseMetrics/deploy ubuntu@<vps>:~/pulsemetrics-deploy`, then `bash setup-server.sh` in that folder.
   It builds `pulsemetrics.env` from the old `/etc/{front,life,burst}pulse/*.env` (same databases, no data copy),
   installs the unit and the `pulsemetrics.fr` vhost with its certificate. The old sites keep running.
2. Declare the Battle.net redirect URLs above, add the `REMOTE_TARGET_PULSEMETRICS` secret, push a `prod-pulsemetrics-x.y.z` tag.
3. Once https://pulsemetrics.fr answers: `bash cutover-legacy.sh`. The old domains (`*pulse.noovio.fr`, `noovio.fr`) redirect
   to the new paths, the old units are stopped and disabled. The old vhosts stay in `sites-available` for a rollback.

### Deploy (GitHub Actions)

`.github/workflows/pulsemetrics.yml` tests, publishes, rsyncs, restarts the service and smoke-tests every tool:

```powershell
git tag prod-pulsemetrics-1.0.0
git push origin prod-pulsemetrics-1.0.0
```

| Secret | Value |
|---|---|
| `REMOTE_HOST` | hostname of the VPS |
| `REMOTE_USER` | `ubuntu` |
| `REMOTE_TARGET_PULSEMETRICS` | `/home/ubuntu/projects/pulsemetrics/` |
| `REMOTE_KEY` | private SSH key whose public half is in `~ubuntu/.ssh/authorized_keys` |

The deploy user restarts the service with `sudo`, without a password.

Check what the process sees (lengths only, never values):

```bash
sudo tr '\0' '\n' < /proc/$(systemctl show -p MainPID --value kestrel-pulsemetrics)/environ \
  | grep -E '^(Blizzard|ConnectionStrings|KeystoneGuru)__' | awk -F= '{ print $1 " -> " length($2) }'
```
