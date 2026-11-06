# Dungeon routes from Keystone.guru

How FrontPulse gets its Mythic+ routes. The engine lives in PulseCore; only applications with `PulseApp.HasDungeonRoutes` use it (FrontPulse today).

## Flow

1. **Season dungeons** come from Raider.io (`ISeasonDungeonSource`), with their timer, short name and artwork.
2. **Slugs** come from `GET /api/v1/dungeon`, matched on `challengeModeId`.
3. **Popular route keys**: `GET /api/v1/route/retail/{dungeon}/popular` first. If it answers 404, the keys are read from the public page `https://keystone.guru/routes/retail/{dungeon}`.
4. **Details**: `GET /api/v1/route/{key}` for up to 15 candidates per dungeon. `RouteSelection.Keep` drops expired, empty and incomplete routes and routes from an older season, then keeps 8.
5. **Storage**: one `season_dungeons` row per (season, dungeon), routes in a jsonb column. A dungeon that fails keeps its previous routes.
6. **Site**: `/routes`, `/routes/{slug}` and a section on the home page, read through `IDungeonRouteQueries` (5 minutes of memory cache).

`RouteSyncWorker` runs this every 6 hours, separately from the ingestion, and counts from the last sync in the database so that a restart does not read everything again. To sync immediately:

```powershell
dotnet run --project FrontPulse/src/Noovio.FrontPulse.Worker -- --sync-routes
```

## Credentials

The API uses HTTP Basic with the email and password of a Keystone.guru account. Only the worker needs them:

- locally, in `dotnet user-secrets` on the Worker project (`KeystoneGuru:Email`, `KeystoneGuru:Password`);
- in production, in `/etc/frontpulse/frontpulse.env` (`KeystoneGuru__Email`, `KeystoneGuru__Password`).

Credentials are sent to the API only, never with the public page requests. Without them, the worker logs a warning and the route pages stay empty.

## What the API really does

Checked on November 6, 2026, against the [documentation](https://keystone.guru/api/documentation) and [its OpenAPI spec](https://keystone.guru/docs?api-docs.json):

| Documented | Observed |
|---|---|
| `GET /route/{gameVersion}/popular`, `/route/{gameVersion}/new`, and the same per dungeon | 404 `{"error":"Not Found"}` for every dungeon and game version tried |
| `GET /route`, filterable by `dungeon_id` | 200 but empty for this account, apparently limited to the account's own routes |
| `GET /route/{route}` | Works for any public route, with its pulls |
| `affixGroups` is an array | It is an object: `expansion`, `season`, `affixes` |
| No stated rate limit | `X-RateLimit-Limit: 900` header, window unknown. The client sends 2 requests per second |

Reading the public page is a fallback, not the design: it depends on the site's HTML. `robots.txt` allows it (`Disallow:` is empty), and it costs 8 pages per sync. It stops by itself once the API answers. If the markup changes, `ParseRouteKeys` finds nothing and the previous routes stay in place, with a warning in the log.

## Terms of use

The [terms](https://keystone.guru/terms) say nothing about the API or iframes, but they do forbid republishing, reproducing or redistributing material from the site "unless content is specifically made for redistribution". Keystone.guru publishes an API and an `embed` link, which suggests this use is welcome, but the terms do not settle it.

**Keystone.guru's agreement has not been obtained.** It should be requested before this goes to production, along with the status of the `popular` endpoints.

To stay close to the source, the site credits Keystone.guru and each author, links to every route, and shows the map in their own embed. There is no MDT string in the API: the embed has its own "Copy MDT" button.

## Tank highlights

`RouteSelection.Highlights` keeps the popularity order and labels what stands out:

- **La plus populaire**: first in the list.
- **Par un tank**: the word "tank" appears in the author or title. This is free text, not verified data.
- **Plus gros pulls en moyenne** and **Pic de pull le plus bas**: computed from the number of enemies per pull, and only when routes actually differ.

Pull sizes count enemies, not damage. A pull with 0 enemies is shown as "aucun ennemi compté"; those pulls are left out of the average.
