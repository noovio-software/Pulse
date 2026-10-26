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

## Production deployment (Linux + Kestrel, GitHub Actions)

The workflow `.github/workflows/altpulse.yml` publishes and deploys on a tag:

```powershell
git tag prod-altpulse-1.0.0
git push origin prod-altpulse-1.0.0
```

**GitHub secrets** (repo > Settings > Secrets and variables > Actions):

| Secret | Value |
|---|---|
| `REMOTE_HOST` | server address |
| `REMOTE_USER` | SSH user used for deployment |
| `REMOTE_KEY` | private SSH key of that user (its public key in `~/.ssh/authorized_keys` on the server) |
| `REMOTE_TARGET_APP` | target folder, e.g. `/var/www/altpulse/` |

The Blizzard credentials are **not** GitHub secrets: they stay on the server.

**One-time server setup:**

1. Blizzard credentials, readable by root only, in `/etc/altpulse/altpulse.env`:
   ```ini
   Blizzard__ClientId=...
   Blizzard__ClientSecret=...
   ```
   `sudo chmod 600 /etc/altpulse/altpulse.env`
2. Service `/etc/systemd/system/altpulse.service`:
   ```ini
   [Unit]
   Description=AltPulse by Noovio
   After=network.target

   [Service]
   WorkingDirectory=/var/www/altpulse
   ExecStart=/usr/bin/dotnet /var/www/altpulse/AltPulse.dll
   Restart=always
   RestartSec=10
   User=www-data
   EnvironmentFile=/etc/altpulse/altpulse.env
   Environment=ASPNETCORE_ENVIRONMENT=Production
   Environment=ASPNETCORE_URLS=http://127.0.0.1:5000

   [Install]
   WantedBy=multi-user.target
   ```
   `sudo systemctl daemon-reload && sudo systemctl enable altpulse`
3. Let the deploy user restart the service without a password, and nothing else (`sudo visudo -f /etc/sudoers.d/altpulse`):
   ```
   <REMOTE_USER> ALL=(root) NOPASSWD: /usr/bin/systemctl restart altpulse
   ```
4. The .NET 10 ASP.NET Core runtime must be installed on the server.

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
