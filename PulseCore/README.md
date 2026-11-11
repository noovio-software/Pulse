# PulseCore

The shared engine behind the role-focused Mythic+ meta sites: [FrontPulse](../FrontPulse/README.md) (tanks), [LifePulse](../LifePulse/README.md) (healers) and [BurstPulse](../BurstPulse/README.md) (DPS).
It is not deployed on its own: each application references it and adds its host, database and theme.

| Project | Depends on | Role |
|---|---|---|
| `Noovio.Pulse.Domain` | nothing | Entities, enums (`WowSpec` = Blizzard spec ids), the role catalog (`PulseRoles.Tank`, `PulseRoles.Healer`, `PulseRoles.Damage`), `PulseApp` |
| `Noovio.Pulse.Application` | Domain | Contracts, ingestion, aggregation, character analysis |
| `Noovio.Pulse.Infrastructure` | Application | Raider.io and Blizzard clients, abstract `PulseDbContext`, stores, cache, `PulseWorker` (worker host) |
| `Noovio.Pulse.Web` | Infrastructure | Razor Class Library: pages, components, Battle.net sign-in, `pulse.css`, `PulseSite` (site host) |
| `Noovio.Pulse.Tests` | Infrastructure | Rules: aggregation, analysis, healer and DPS roles |

The layers are separate projects so that the rules stay free of EF Core and ASP.NET, as they were in FrontPulse.

## What an application provides

```csharp
public static readonly PulseApp Definition = new("LifePulse", PulseRoles.Healer, "<sigil path>");

public sealed class LifePulseDbContext(DbContextOptions<LifePulseDbContext> options) : PulseDbContext(options);

await PulseSite.RunAsync<LifePulseDbContext>(args, LifePulseApp.Definition);    // Web/Program.cs
await PulseWorker.RunAsync<LifePulseDbContext>(args, LifePulseApp.Definition);  // Worker/Program.cs
```

Plus its migrations, `appsettings`, `wwwroot/theme.css` and favicon, and deployment files. Everything named after the application derives from `PulseApp.Name`: configuration sections (`LifePulse:Ingestion`, `LifePulse:Meta`), connection string, cookie, Redis prefix.

The host's Web project sets `RequiresAspNetWebAssets` to `true`: its pages live in the library, so the SDK would otherwise leave out `_framework/blazor.web.js` and nothing would become interactive.

## Optional features

`PulseApp` also switches features on per application. `HasDungeonRoutes` (FrontPulse only) enables the Keystone.guru sync, the `/routes` pages and their links: see [the routes doc](docs/keystone-guru-routes.md). The `season_dungeons` table exists in every application's database, because the model is shared, but it stays empty elsewhere.

## What a role defines

`PulseRole` holds what differs between roles, as data:

| Member | Tank | Healer | DPS |
|---|---|---|---|
| `RaiderIoRole` | `tank` | `healer` | `dps` |
| `Kind` | ranks, avoidance lines of the sheet | ranks, healing versatility, mana | ranks |
| `Specs` | 6 specs | 7 specs (priest has two: `ForClass` returns a list, the analysis takes `?spec=`) | 27 specs, up to three per class; above 8 specs the header shows a menu grouped by class |
| `StatBasis` | melee lines of the character sheet | spell lines | melee lines, overridden per spec by `SpecInfo.Basis` (spell for casters, ranged for Beast Mastery and Marksmanship) |
| `JournalHeadings` | tank sections of the Encounter Journal | healer sections | DPS sections |
| `TrinketKeywords` | survival effects | healing and mana effects | effects that deal damage (the others are stat trinkets) |
| `BossHits` | heavy hits on the tank (2 signals needed) | group damage, dispels, healing absorbs (1 signal is enough) | adds, targets to kill, shields to break (1 signal is enough) |
| `Wording` | "tank", "bijou défensif"… | "soigneur", "bijou de soin"… | "DPS", "bijou de dégâts"… |

## Theming

`pulse.css` declares the colour tokens (`--accent`, `--accent-text`, `--accent-alt`, `--cta`…) and each application's `theme.css`, loaded after it, redefines them. `--accent` is for fills; `--accent-text` is for text and may be lighter so it stays readable on the dark background.

## Tests

```powershell
dotnet test PulseCore/tests/Noovio.Pulse.Tests
```
