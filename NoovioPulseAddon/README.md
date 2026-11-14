# NoovioPulse Addons

World of Warcraft addon that exports the logged-in character to the Pulse sites
([FrontPulse](../FrontPulse/README.md), [LifePulse](../LifePulse/README.md), [BurstPulse](../BurstPulse/README.md)),
in the spirit of SimulationCraft: a string copied in game, pasted on the site, which runs the character analysis against the top 100.

What the import adds to the Battle.net sign-in:

- the character as it is **now**, without waiting for Blizzard to refresh its profile (logout);
- no account on the site: `/importer` is public, and the analysis link it gives can be shared (kept 7 days);
- any character, including one from another account.

The portrait and the Mythic+ part (Raider.io) are read by name, realm and region; when they are missing, the analysis goes on without them.

## Install

1. Download the zip of the latest version (GitHub releases `noovio-pulse-addon-x.y.z`).
2. Drop the `NoovioPulse` folder into `World of Warcraft\_retail_\Interface\AddOns`.
3. In game: `/noovio` (or the addon menu under the minimap), out of combat, in the specialization to analyse.
4. `Ctrl+C` (the string is already selected), then paste it on `https://<site>.noovio.fr/importer`.

While developing, a symbolic link saves copying the folder:

```powershell
New-Item -ItemType SymbolicLink -Path "C:\Program Files (x86)\World of Warcraft\_retail_\Interface\AddOns\NoovioPulse" -Target "$PWD\NoovioPulseAddon\NoovioPulse"
```

## Files

| File | Role |
|---|---|
| `NoovioPulse.toc` | Addon declaration: interface versions, load order, addon menu entry |
| `Locales.lua` | Window texts, in French (English for other clients) |
| `Json.lua` | JSON and Base64 encoder in plain Lua |
| `Export.lua` | Reads the character: gear, talents, stats, character sheet; encodes the string |
| `Inventory.lua` | Reads the bags (gear, consumables) and the Great Vault for the character export |
| `Alts.lua` | Saves each character's progress at login and when it changes (vault, keys, keystone) for AltPulse |
| `UI.lua` | Window (banner, two cards, copy screen), `/noovio` command, addon menu |
| `Minimap.lua` | Minimap button (drag to move, `/noovio minimap` to hide) |
| `Media/*.tga` | Icon, banner and card art, rendered from `art/*.svg` (32-bit RLE TGA, power-of-two sizes) |

Commands: `/noovio` opens the window, `/noovio perso` and `/noovio alts` go straight to an export,
`/noovio minimap` toggles the button, `/noovio oublier` clears the saved alts.

`## Interface` lists the supported client versions: add each new patch, or the addon shows as out of date.

## String format

`NP1:<mode>:<base64>`

| Mode | Content |
|---|---|
| `D` | JSON compressed by the client (`C_EncodingUtil.CompressString`, deflate): a few KB |
| `J` | Plain JSON, when the client cannot compress |

The site (`CharacterImportParser`, in PulseCore) accepts raw deflate, zlib or gzip in mode `D`, based on the header,
and ignores the spaces and line breaks a paste may add.

The JSON (format `v: 1`), texts in the client's language:

```jsonc
{
  "v": 1, "addon": "1.0.0", "game": "12.0.5.60000", "locale": "frFR", "exported_at": 1794650000,
  "character": { "name": "Aelys", "realm": "Conseil des Ombres", "region": "eu", "class": 5, "spec": 258, "spec_name": "Ombre", "level": 90, "ilvl": 701.4 },
  "gear": [
    {
      "slot": "HEAD",                      // Blizzard API slot type
      "id": 212345, "name": "…", "ilvl": 710, "bonus": [10390, 6652],
      "enchant_id": 7000, "enchant": "Enchanté : +80 Intelligence",
      "gems": [{ "id": 213743, "name": "…", "text": "+147 Maîtrise" }, {}],   // {}: empty socket
      "stats": { "MASTERY_RATING": 600, "HASTE_RATING": 300 },               // the item's own ratings
      "effects": [{ "trigger": "use", "spell": 5001, "name": "…", "text": "… (2 min de recharge)" }],
      "embellished": true
    }
  ],
  "talents": {
    "code": "C4DAAA…",                    // the game's export string
    "hero": "Tisse-Vide", "hero_id": 9,
    "nodes": [[node, entry, spell, rank, granted]]   // granted = 1: node given for free
  },
  "stats": {                               // the site keeps the lines of the spec (spell, ranged, melee)
    "haste": { "rating": 900, "melee": 10, "ranged": 11, "spell": 22.5 },
    "crit": { "rating": 500, "melee": 12, "ranged": 13, "spell": 18.25 },
    "mastery": { "rating": 700, "percent": 40 },
    "versatility": { "rating": 300, "done": 6, "taken": 3 }
  },
  "sheet": { "health": 800000, "strength": 900, "agility": 1000, "stamina": 50000, "intellect": 40000, "mana": 250000,
             "armor": 5000, "dodge": 3, "parry": 0, "block": 0, "leech": 1, "avoidance": 2, "speed": 4 }
}
```

The character export also carries the bags and the Great Vault (`Inventory.lua`), analysed in the « Sacs et coffre »
block of the report: bag pieces that beat the equipped one (keeping set pieces and embellishments unless the gain is
large; trinkets and weapons follow the top's picks), the vault choice to make, and the consumables ready for a key.

```jsonc
"bags": {
  "gear": [{ "slot": "LEGS", "id": 244580, "name": "…", "ilvl": 334, "bonus": [], "stats": {}, "set_id": null, "usable": true, "bound": true }],
  "consumables": [{ "id": 212283, "name": "…", "kind": "flask", "count": 3, "quality": 2, "current": true, "use": "…" }]
  // kind: potion, elixir, flask, food, enhancement (oils, stones), other (runes); "use": the item's « Use: » text
},
"vault": {
  "has_rewards": true, "can_claim": true,
  "activities": [{ "type": 1, "index": 1, "threshold": 1, "progress": 4, "level": 12, "example_ilvl": 341,
                   "items": [{ "slot": "FEET", "id": 268231, "name": "…", "ilvl": 341 }] }]   // items: only when the vault is ready
}
```

Equipped pieces also carry `set_id` (tier set). Every piece (worn, in bags, in the vault) carries its upgrade track,
read from the tooltip line « Upgrade Level: Hero 2/6 » (the pattern comes from the game's own string, so any language
works): `"upgrade": { "track": "Héros", "rank": 2, "max_rank": 6, "max_ilvl": 346 }`. `max_ilvl` comes from
`C_ItemUpgrade.GetItemUpgradeItemInfo` when the item has a location (worn or in bags); for vault items the site takes the
max of another piece on the same track, or the step between two ranks, or 3 levels per rank. Pieces are compared at
their max level, not at their drop level: a Hero 1/6 piece beats a Champion 6/6 one that cannot go higher. Exports from an older addon, without these fields, are still analysed.

Every export carries `"kind"`: `"character"` (above, for FrontPulse, LifePulse and BurstPulse) or `"alts"` (for AltPulse).
Each site refuses the other kind with a message telling where to paste it.

The `alts` export lists every character seen on the account (saved in `NoovioPulseDB.characters`):

```jsonc
{
  "v": 1, "kind": "alts", "addon": "1.0.0", "exported_at": 1794650000,
  "characters": [{
    "name": "Aelys", "realm": "Conseil des Ombres", "region": "eu", "class": 5, "level": 90,
    "spec": 258, "spec_name": "Ombre", "role": "DAMAGER", "ilvl": { "equipped": 701.4, "overall": 702.1 },
    "rating": 2450, "rating_color": "#a335ee",
    "keystone": { "map": 503, "map_name": "…", "level": 11 },
    "week_start": 1794304400,              // weekly reset the data belongs to
    "has_rewards": false,
    "vault": [{ "type": 1, "index": 1, "threshold": 1, "progress": 4, "level": 12 }],   // C_WeeklyRewards: 1 dungeons, 3 raid (level = difficulty id), 6 world
    "runs": [{ "map": 502, "map_name": "…", "level": 12, "completed": false }],
    "saved_at": 1794650000
  }]
}
```

AltPulse (`POST /api/addon`, button « MàJ depuis l'addon ») adds these characters to the grid and keeps the data in
the browser: for the current week it replaces the vault, the keys of the week and the keystone; a week old, only the
« last week » dungeon row; older, it is ignored.

A field added without changing the meaning of the others keeps `v: 1` (the site ignores what it does not know). An
incompatible change moves to `v: 2`, and the site refuses versions it does not know yet with a clear message.

## On the site

Everything lives in PulseCore, so the three sites share it:

| Where | What |
|---|---|
| `Noovio.Pulse.Application/Import` | `CharacterImportParser` (string → `CharacterImport`), `ImportedCharacterMapper` (→ `CharacterDetails`) |
| `CharacterAnalysisService.AnalyzeImportAsync` | Same analysis as an account character |
| `Noovio.Pulse.Web`: `/importer`, `/importer/{key}` | Paste (plain form post), then report (`AnalysisReport`, shared with the account page) |

What the analysis compares by name is matched to the meta by id: talents and hero tree through the spec's talent tree
(node ids are the same in game and in the API), embellishments by spell or item. Enchants and gems read in French as in English.

## Release

```powershell
git tag noovio-pulse-addon-1.0.0
git push origin noovio-pulse-addon-1.0.0
```

The `noovio-pulse-addon.yml` workflow runs the import tests, replaces `@project-version@` in the `.toc`, zips the
`NoovioPulse` folder and publishes it as a GitHub release.
