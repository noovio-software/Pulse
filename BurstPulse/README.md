# BurstPulse by Noovio

Mythic+ meta for World of Warcraft damage dealers: gear, secondary stats, talents, trinkets and embellishments of the top 100 DPS of each of the 27 damage specs
(every class, Devourer included), and a comparison of your own character against them.

It does what [FrontPulse](../FrontPulse/README.md) does for tanks and [LifePulse](../LifePulse/README.md) for healers, on the same engine: see [PulseCore](../PulseCore/README.md). This folder only holds the host, migrations, theme and deployment files.

| Project | Role |
|---|---|
| `Noovio.BurstPulse.Infrastructure` | `BurstPulseApp` (name, role, sigil), `BurstPulseDbContext`, migrations |
| `Noovio.BurstPulse.Worker` | Ingestion every 6 hours, applies migrations at startup |
| `Noovio.BurstPulse.Web` | The site: `Program.cs`, `wwwroot/theme.css` (Midnight violet `#7C3AED`, fuchsia second), favicon |

## Run it

```powershell
# PostgreSQL + Redis. Same ports as FrontPulse's and LifePulse's compose files: run one at a time, or point the connection strings at existing instances.
docker compose -f BurstPulse/docker-compose.yml up -d

# Blizzard key, for the worker (ingestion) and the web (sign-in, analysis)
foreach ($p in 'Worker', 'Web') {
  dotnet user-secrets set "Blizzard:ClientId" "<client-id>" --project BurstPulse/src/Noovio.BurstPulse.$p
  dotnet user-secrets set "Blizzard:ClientSecret" "<client-secret>" --project BurstPulse/src/Noovio.BurstPulse.$p
}

dotnet run --project BurstPulse/src/Noovio.BurstPulse.Worker   # migrations, then ingestion at startup and every 6 h
dotnet run --project BurstPulse/src/Noovio.BurstPulse.Web --launch-profile http
# → http://localhost:5179
```

**Battle.net client**: add these redirect URLs to the client.

| Host | Redirect URL |
|---|---|
| Local | `http://localhost:5179/signin-battlenet` |
| Production | `https://burstpulse.noovio.fr/signin-battlenet` |

If BurstPulse shares FrontPulse's or LifePulse's Blizzard client, the workers share its hourly quota (36,000 requests): each one limits itself to 35,000, so a dedicated client is safer if their ingestions overlap. BurstPulse is the heaviest of the three: 27 specs × 130 candidates × 3 profile calls is about 10,500 requests per ingestion.

## Deploy

Same model as FrontPulse: port 8550 behind Nginx, systemd units `kestrel-burstpulse` and `kestrel-burstpulse-worker`.

1. Point the DNS record `burstpulse.noovio.fr` at the server.
2. Run `BurstPulse/deploy/setup-server.sh` on the server, then fill in `/etc/burstpulse/burstpulse.env`.
3. Add the GitHub secret `REMOTE_TARGET_BURSTPULSE` (ending in `/`).
4. Push a tag `prod-burstpulse-x.y.z`.
