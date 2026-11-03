# FrontPulse by Noovio

Mythic+ meta for World of Warcraft tanks: gear, secondary stats, talents, trinkets and embellishments
of the top 100 tanks of each spec (Blood, Vengeance, Guardian, Brewmaster, Protection Paladin, Protection Warrior).

- **Stack**: .NET 10 (same SDK and VPS target as AltPulse), Blazor Web App in Interactive Server mode, EF Core + PostgreSQL, Redis (`IDistributedCache`).
- **Data**: [Raider.io](https://raider.io/api) (season run leaderboard) + [Blizzard API](https://develop.battle.net) (equipment, specializations, statistics, item icons).
- **Principle**: the worker precomputes one snapshot per spec every 6 hours; the site only reads those snapshots.

## Architecture

| Project | Role |
|---|---|
| `Noovio.FrontPulse.Domain` | Entities (`TankProfile`, `SpecMeta` + `SpecMetaSnapshot`, `DungeonRoute`, `ItemStatSnapshot`), enums (`TankSpec` = Blizzard spec ids, `SecondaryStat`, `MythicAffix`, `GearSlot`) |
| `Noovio.FrontPulse.Application` | Contracts (`ILeaderboardSource`, `ICharacterLoadoutSource`, `IItemMediaSource`, `ITankMetaStore`, `ISpecMetaQueries`), `TankMetaAggregatorService` (pure computation), `TankMetaIngestionService` (orchestration) |
| `Noovio.FrontPulse.Infrastructure` | `RaiderIoClient`, `BlizzardOAuthClient` (token in memory + Redis), `BlizzardClient`, rate limiters, `FrontPulseDbContext` (jsonb columns), migrations |
| `Noovio.FrontPulse.Worker` | `MetaIngestionWorker` (`BackgroundService`, `PeriodicTimer`), applies migrations at startup |
| `Noovio.FrontPulse.Web` | Blazor site: `/` (spec cards), `/specs/{slug}` (`SpecOverview`), `/compte` (characters), `/compte/personnages/{id}/analyse` (`CharacterReport`), Battle.net sign-in (`Auth/`), `/api/specs` and `/api/specs/{slug}` (JSON) |

### Ingestion pipeline

1. **Raider.io**: `GET /api/v1/mythic-plus/runs?season={season}&region=world&dungeon=all&page={n}` (20 runs per page; `region=all` is rejected with a 400).
   Each `role == "tank"` character is kept on its first appearance (its best key), keyed by `spec.id`, up to `CandidatesPerSpec` (130) per spec. CN characters are skipped (separate Blizzard gateway). The talent string seen on that key (`roster[].loadout`) is the reference build.
2. **Blizzard**: `equipment`, `specializations` and `statistics` per character. Characters whose active spec is no longer the tank spec are dropped, then the first 100 are kept and re-ranked.
   Rate limit: token bucket at 90 req/s + one-hour sliding window at 35,000 req (just under the 100/s and 36,000/h quotas), plus retries on 429.
3. **Aggregation**: share of tanks wearing each item per slot, top 5 trinkets per orientation, medians of the 4 secondary stats (rating, %, share), top 3 embellishment combos, top 3 talent strings, hero trees, pick rate per talent.
4. **Storage**: per (season, spec), the profiles and the snapshot are replaced in a single transaction. If a spec resolves no profiles, the previous snapshot is kept.

Defensive vs offensive trinkets: Blizzard does not expose a role, so a trinket is defensive when one of its effects mentions `absorb`, `damage taken`, `heal`… (see `MetaOptions.DefensiveKeywords`). Misclassifications are fixed with `FrontPulse:Meta:DefensiveTrinketIds` / `OffensiveTrinketIds`.

## Account and character analysis

- **Sign-in with Battle.net only** (OAuth authorization code, scopes `openid wow.profile`), then a 30-day cookie. No password is stored: the account is keyed by the Battle.net id (`user_accounts`).
- **Characters**: listed from `/profile/user/wow` in every region (eu, us, kr, tw) at each sign-in, and on demand with the *Synchroniser* button; stored in `user_characters`. The user token (24 h) stays in the encrypted cookie; once it has expired, *Synchroniser* goes back through Battle.net (consent already given, so no prompt).
- **Analyser** (`/compte/personnages/{id}/analyse`): compares a character with the meta of its class's tank spec, using Blizzard (equipment, talents of the tank spec even when another spec is active, full character sheet, portrait) and Raider.io (score, ranks, best key per dungeon). Output: a score out of 100 (each area counts 1 / 0.5 / 0), one verdict per area, and an action plan *what / why / how*, plus the visual views: stats on the top's range, gear per slot, the player's talent tree compared to the top, Mythic+ per dungeon, full sheet. Rules and thresholds live in `CharacterAnalyzer` (pure, unit-tested); results are cached 10 minutes.

**Battle.net client**: the sign-in uses the same client as the API calls (`Blizzard:ClientId` / `Blizzard:ClientSecret`, now also needed by the Web project). Its redirect URLs must be declared on https://develop.battle.net/access/clients, one per host:

| Host | Redirect URL |
|---|---|
| Local | `http://localhost:5177/signin-battlenet` |
| Production | `https://<frontpulse host>/signin-battlenet` |

## Run it

```powershell
# PostgreSQL + Redis (or point the connection strings at existing instances)
docker compose -f FrontPulse/docker-compose.yml up -d

# Blizzard key, for the worker (ingestion) and the web (sign-in, analysis)
foreach ($p in 'Worker', 'Web') {
  dotnet user-secrets set "Blizzard:ClientId" "<client-id>" --project FrontPulse/src/Noovio.FrontPulse.$p
  dotnet user-secrets set "Blizzard:ClientSecret" "<client-secret>" --project FrontPulse/src/Noovio.FrontPulse.$p
}

dotnet run --project FrontPulse/src/Noovio.FrontPulse.Worker   # migrations, then ingestion at startup and every 6 h
# Recompute the snapshots from the stored profiles only (seconds), e.g. after changing an aggregation rule:
dotnet run --project FrontPulse/src/Noovio.FrontPulse.Worker -- --rebuild-snapshots
dotnet run --project FrontPulse/src/Noovio.FrontPulse.Web --launch-profile http
# → http://localhost:5177
```

Without `ConnectionStrings:Redis` (the default in Development), the distributed cache falls back to in-process memory.
With the compose Redis: `dotnet user-secrets set "ConnectionStrings:Redis" "localhost:6379" --project <Worker or Web>`.

Tests: `dotnet test FrontPulse/tests/Noovio.FrontPulse.Application.Tests`.

## Not done yet

- **MDT routes**: the `DungeonRoute` entity and the `dungeon_routes` table exist, but there is no dungeon page and no import/sync yet. The Keystone.guru API still has to be checked before writing `KeystoneGuruClient`.
- **Talent tree in its real layout**: the grid shows nodes by pick rate. Placing them like the in-game tree needs the Game Data API `talent-tree` endpoints.
- **Shared design system**: layout, `TopBar`, `Sigil` and the atoms (`.panel`, `.btn`, `.eyebrow`…) follow AltPulse's structure, but they are copies. A `Noovio.Ui` RCL shared by both apps would remove the duplication.
- **Production deployment**: no workflow or systemd unit yet (AltPulse's `.github/workflows/altpulse.yml` is the model to reuse).
