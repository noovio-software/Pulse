# Front · PulseMetrics

Mythic+ meta for World of Warcraft tanks: gear, secondary stats, talents, trinkets and embellishments
of the top 100 tanks of each spec (Blood, Vengeance, Guardian, Brewmaster, Protection Paladin, Protection Warrior).

- **Stack**: .NET 11, Blazor Web App in Interactive Server mode, EF Core + PostgreSQL, Redis (`IDistributedCache`).
- **Data**: [Raider.io](https://raider.io/api) (season run leaderboard) + [Blizzard API](https://develop.battle.net) (equipment, specializations, statistics, item icons).
- **Routes**: [Keystone.guru](https://keystone.guru) (popular routes of each season dungeon, with the size of each pull), see [the routes doc](../../PulseCore/docs/keystone-guru-routes.md).
- **Principle**: the worker precomputes one snapshot per spec every 6 hours; the site only reads those snapshots.

## Architecture

The engine is shared with Life and Burst and lives in [PulseCore](../../PulseCore/README.md) (rules, API clients, pages, workers). What is specific to Front:

| Code | Role |
|---|---|
| `PulseMetrics/Front` | `FrontPulseApp` (name, tank role, sigil, dungeon routes on), `FrontPulseDbContext`, migrations |
| `PulseMetrics/wwwroot/front` | `theme.css`, favicon |

The site, its ingestion worker and its route sync run in the PulseMetrics process, under `/front/` (see [the PulseMetrics README](../README.md)).

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

The report is organized around what to do next:

- **Action plan**, grouped by effort: fix now (gems, enchants, talents), craft, farm, play.
- **Gems and enchants** (`GemEnchantOptimizer`): each socket and enchant against the top, rating per origin (pieces, gems, enchants), and a greedy search for the swaps that bring the four stats back into the top's range. It only proposes choices the top really uses, and never replaces a gem without readable stats. Projected percentages are estimates: the rating → % conversion is a regression on the top's sheets, ignoring diminishing returns.
- **Farm roadmap** (`FarmRoadmapBuilder`): pieces to get per slot, grouped by dungeon or raid in order of priority (slot weight, share in the top, item level gap, low stats it raises, dungeon also behind in the player's M+ progression). Sources come from the Encounter Journal (`BlizzardJournalSource`, season dungeons and expansion raids listed by Raider.io, cached 1 day); items it does not list (crafts, catalyst) are shown apart.
- **Trinkets** (`TrinketPlanner`): a verdict first (keep or replace, against the pairs the top plays), where the replacement drops, how to play each trinket (read from its effect text), then the boss hits to anticipate. Every encounter has a "Tank" section in the Journal, so a boss only counts when a bullet of that section describes a hit (physical or melee damage on the target, heavy or increasing); the bullet is quoted as is. It is a reading aid, not a damage measurement: the API has no difficulty field, and a real ranking would need combat logs (Warcraft Logs).

Gems and per-piece stats are stored on the top's profiles (`GearPiece.Sockets`, `GearPiece.Stats`): the comparison with the top fills in after the first ingestion following this change. Until then the panel says so and only reads the player's pieces.

**Battle.net client**: the sign-in uses the same client as the API calls (`Blizzard:ClientId` / `Blizzard:ClientSecret`). Its redirect URLs (`http://localhost:8580/front/signin-battlenet`, `https://pulsemetrics.fr/front/signin-battlenet`) must be declared on https://develop.battle.net/access/clients.

## Run it

See [the PulseMetrics README](../README.md). Without `ConnectionStrings:Redis` (the default in Development), the distributed cache falls back to in-process memory. Tests: `dotnet test PulseCore/tests/Noovio.Pulse.Tests`.

## Not done yet

- **Talent tree in its real layout**: the grid shows nodes by pick rate. Placing them like the in-game tree needs the Game Data API `talent-tree` endpoints.
