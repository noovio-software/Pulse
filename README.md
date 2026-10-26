# AltPulse by Noovio

Track your World of Warcraft characters in a single grid (one column per character),
with item level, Mythic+ score, keys done this week, Great Vault, best key per dungeon for the season,
and raid progression and lockouts for the week, per difficulty.

- **Stack**: ASP.NET Core / Blazor Web App .NET 10, `InteractiveAuto` render mode (Interactive Server on the first visit, then WebAssembly once the runtime is cached).
- **Data**: [Raider.io API](https://raider.io/api) (no key needed) + [Blizzard API](https://develop.battle.net) (optional, recommended).
- **History**: searched characters and display preferences live only in the browser's `localStorage` (`altpulse.history`, `altpulse.settings`).
- **Theme**: Midnight Season 2, *The Curse of Ula'tek*, The Venomous Abyss.

## Run it

```powershell
dotnet run --project AltPulse --launch-profile http
# → http://localhost:5166
```

## Blizzard key (optional)

Without a key, the app works on Raider.io alone. The Blizzard key adds:
- the **raid lockouts for the week** (per boss and per difficulty) and therefore the **raid row of the Great Vault**;
- level, French class and spec names, average item level, detailed gear with enchants;
- autocomplete of the realm list.

Create a client at https://develop.battle.net/access/clients, then:

```powershell
dotnet user-secrets set "Blizzard:ClientId" "<client-id>" --project AltPulse
dotnet user-secrets set "Blizzard:ClientSecret" "<client-secret>" --project AltPulse
```

The secret stays server-side: the WASM front end only calls the `/api/*` endpoints.

## Production (https://altpulse.noovio.fr)

Hosted on the OVH VPS `<vps-host>` (user `ubuntu`), alongside Nowtilus, WattsIn, Aidalap…

| Piece | Location |
|---|---|
| App files | `/home/ubuntu/projects/altpulse/` |
| systemd unit | `/etc/systemd/system/kestrel-altpulse.service` (Kestrel on `127.0.0.1:8500`) |
| Blizzard credentials | `/etc/altpulse/altpulse.env`, `root:root 600`, loaded by `EnvironmentFile=` |
| Nginx vhost | `/etc/nginx/sites-available/altpulse` (WebSocket upgrade for Blazor, HTTP → HTTPS) |
| TLS certificate | Let's Encrypt `altpulse.noovio.fr`, renewed by certbot |

Unlike the other units of the VPS, the secrets are **not** in `Environment=` lines: units are world-readable (`644`), the env file is not.

To change a Blizzard credential: `sudo nano /etc/altpulse/altpulse.env` then `sudo systemctl restart kestrel-altpulse`.
Check what the process actually sees (lengths only, never values):

```bash
sudo tr '\0' '\n' < /proc/$(systemctl show -p MainPID --value kestrel-altpulse)/environ \
  | grep '^Blizzard__' | awk -F= '{ print $1 " -> " length($2) }'
```

### Deploy (GitHub Actions)

The workflow `.github/workflows/altpulse.yml` publishes, rsyncs, restarts the service and smoke-tests the site on a tag:

```powershell
git tag prod-altpulse-1.0.1
git push origin prod-altpulse-1.0.1
```

GitHub secrets (repo > Settings > Secrets and variables > Actions):

| Secret | Value |
|---|---|
| `REMOTE_HOST` | `<vps-host>` |
| `REMOTE_USER` | `ubuntu` |
| `REMOTE_TARGET_APP` | `/home/ubuntu/projects/altpulse/` |
| `REMOTE_KEY` | private SSH key whose public half is in `~ubuntu/.ssh/authorized_keys` |

`ubuntu` has passwordless sudo on this VPS, so the workflow can restart the service without a password.
## Architecture

| Project | Role |
|---|---|
| `AltPulse.Client` | Interactive components (`Pages/Home.razor`, `CharacterGrid`, `EquipmentDialog`), shared models, `IAltPulseApi` contract, `HttpAltPulseApi` (WASM), `LocalHistoryStore` (localStorage) |
| `AltPulse` | Host, `/api/season/{region}`, `/api/realms/{region}` and `/api/characters/{region}/{realm}/{name}` endpoints, `RaiderIoClient`, `BlizzardClient` (cached OAuth token), `AltPulseService` (aggregation and in-memory cache) |

`IAltPulseApi` is implemented twice: `AltPulseService` directly on the server side (prerendering and Interactive Server), and `HttpAltPulseApi` on the WebAssembly side.

## Known limits (public APIs)

Some in-game data is not exposed by any web API, so it is shown as "n/d" or left out:
the keystone held in the bags, crests and currencies, the World/Delves row of the Vault, and whether the Vault was already claimed.
The "Last week (M+)" row shows the rewards earned last week.
