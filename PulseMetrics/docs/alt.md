# Alt · PulseMetrics

Track your World of Warcraft characters in a single grid (one column per character),
with item level, Mythic+ score, keys done this week, Great Vault, best key per dungeon for the season,
and raid progression and lockouts for the week, per difficulty. Served at `/alt/`.

- **Stack**: Blazor Web App .NET 10, `InteractiveAuto` render mode (Interactive Server on the first visit, then WebAssembly once the runtime is cached).
- **Data**: [Raider.io API](https://raider.io/api) (no key needed) + [Blizzard API](https://develop.battle.net) (optional, recommended).
- **History**: searched characters and display preferences live only in the browser's `localStorage` (`altpulse.history`, `altpulse.settings`). It is per origin: history kept on `altpulse.noovio.fr` does not follow to `pulsemetrics.fr`.
- **Theme**: Midnight Season 2, *The Curse of Ula'tek*, The Venomous Abyss.

Running and deploying: see [the PulseMetrics README](../README.md).

## Blizzard key (optional)

Without a key, Alt works on Raider.io alone. The Blizzard key (`Blizzard:ClientId` / `Blizzard:ClientSecret`, shared with Front, Life and Burst) adds:
- the **raid lockouts for the week** (per boss and per difficulty) and therefore the **raid row of the Great Vault**;
- level, class and spec names in the interface language, average item level, detailed gear with enchants;
- autocomplete of the realm list.

The secret stays server-side: the WASM front end only calls the `/alt/api/*` endpoints.

## Architecture

| Code | Role |
|---|---|
| `PulseMetrics.Alt.Client` | Interactive components (`Pages/Home.razor`, `CharacterGrid`, `EquipmentDialog`), shared models, `IAltPulseApi` contract, `HttpAltPulseApi` (WASM), `LocalHistoryStore` (localStorage) |
| `PulseMetrics/Alt` | `AltSite` (services, `/api/season/{region}`, `/api/realms/{region}`, `/api/characters/{region}/{realm}/{name}`, `/api/addon`), page shell, `RaiderIoClient`, `BlizzardClient` (cached OAuth token), `AltPulseService` (aggregation and in-memory cache) |

`IAltPulseApi` is implemented twice: `AltPulseService` directly on the server side (prerendering and Interactive Server), and `HttpAltPulseApi` on the WebAssembly side.

## Languages

French (default) and English, in `PulseMetrics.Alt.Client/Resources/AltTexts(.en).resx` (public generated class, also used by the server for its messages). The server picks the language from the `.AspNetCore.Culture` cookie (`GET /alt/culture/{fr|en}`, shared by every tool of `pulsemetrics.fr`) or `Accept-Language`, and writes it in `<html lang>`; the WebAssembly client reads it there at startup (`altpulse.culture()` in `wwwroot/alt/app.js`) before `RunAsync`, which then loads the English satellite assembly. `BlazorWebAssemblyLoadAllGlobalizationData` keeps both cultures' formats available in the browser.

Season and character caches are kept per language: the interface language also selects the Blizzard (`fr_FR` / `en_US`) and Raider.io locales, so class, spec, item and affix names follow it. The realm list stays in `Blizzard:Locale`, because the addon export gives the realm name as displayed in a French client.

## Known limits (public APIs)

Some in-game data is not exposed by any web API, so it is shown as "n/d" or left out:
the keystone held in the bags, crests and currencies, the World/Delves row of the Vault, and whether the Vault was already claimed.
The "Last week (M+)" row shows the rewards earned last week.
