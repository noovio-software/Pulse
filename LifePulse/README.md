# LifePulse by Noovio

Mythic+ meta for World of Warcraft healers: gear, secondary stats, talents, trinkets and embellishments of the top 100 healers of each spec
(Restoration Shaman, Restoration Druid, Preservation, Mistweaver, Holy Paladin, Discipline, Holy Priest), and a comparison of your own character against them.

It does what [FrontPulse](../FrontPulse/README.md) does for tanks, on the same engine: see [PulseCore](../PulseCore/README.md). This folder only holds the host, migrations, theme and deployment files.

| Project | Role |
|---|---|
| `Noovio.LifePulse.Infrastructure` | `LifePulseApp` (name, role, sigil), `LifePulseDbContext`, migrations |
| `Noovio.LifePulse.Worker` | Ingestion every 6 hours, applies migrations at startup |
| `Noovio.LifePulse.Web` | The site: `Program.cs`, `wwwroot/theme.css` (shaman blue `#0070DD`), favicon |

## Run it

```powershell
# PostgreSQL + Redis. Same ports as FrontPulse's compose file: run one at a time, or point the connection strings at existing instances.
docker compose -f LifePulse/docker-compose.yml up -d

# Blizzard key, for the worker (ingestion) and the web (sign-in, analysis)
foreach ($p in 'Worker', 'Web') {
  dotnet user-secrets set "Blizzard:ClientId" "<client-id>" --project LifePulse/src/Noovio.LifePulse.$p
  dotnet user-secrets set "Blizzard:ClientSecret" "<client-secret>" --project LifePulse/src/Noovio.LifePulse.$p
}

dotnet run --project LifePulse/src/Noovio.LifePulse.Worker   # migrations, then ingestion at startup and every 6 h
dotnet run --project LifePulse/src/Noovio.LifePulse.Web --launch-profile http
# → http://localhost:5178
```

**Battle.net client**: add these redirect URLs to the client.

| Host | Redirect URL |
|---|---|
| Local | `http://localhost:5178/signin-battlenet` |
| Production | `https://lifepulse.noovio.fr/signin-battlenet` |

If LifePulse shares FrontPulse's Blizzard client, the two workers share its hourly quota (36,000 requests): each one limits itself to 35,000, so a dedicated client is safer if their ingestions overlap.

## Deploy

Same model as FrontPulse: port 8530 behind Nginx, systemd units `kestrel-lifepulse` and `kestrel-lifepulse-worker`.

1. Point the DNS record `lifepulse.noovio.fr` at the server.
2. Run `LifePulse/deploy/setup-server.sh` on the server, then fill in `/etc/lifepulse/lifepulse.env`.
3. Add the GitHub secret `REMOTE_TARGET_LIFEPULSE` (ending in `/`).
4. Push a tag `prod-lifepulse-x.y.z`.
