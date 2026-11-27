# Level zones v1.3

The mode divides Tamriel into MMO level zones, with Cheydinhal as the starting region and harder enemies farther
away. Tamriel borders come from the game's regions in the CC0 [OB:MP asset catalog](https://catalog.obmp.readym.io/).
The Blue Road keeps its hand-drawn corridor. Staff can inspect the result in game with `/azone`.
The existing `/aspawn here` command sets the new-character spawn in Cheydinhal. Zones do not overwrite a saved spawn.

`/zone` names the current zone and its level range. Entering a different zone shows the same text for five seconds
in the centre of the game's screen, in the player's language. The server checks once a second while the player is
playing. Leaving all zones shows nothing, and the last zone shown is kept, so stepping out of a house or a dungeon
back into the same zone shows nothing either. `/azone` requires the staff permission `azone` and duty, under the usual
owner exception. It reports the area id, cell kind and coordinates, zone, current cell level, session level and
each spawner's reference id and offset. It is a read-only command.
The LevelZones migration grants `azone` to administrators and keeps their existing permissions. Run the migrator
before starting this build. Owners already hold every permission. Other roles need an explicit grant.

## Configuration

`zones` in the mod's `server/config.json` has `enabled` (true), `takenPerLevel` (0.08), `maxTaken` (3.0),
`dealtPerLevel` (0.05), `minDealt` (0.35) and `entries`, an ordered list. The source file is
`src/CyrodiilRP.Server/config.json`. Edit the deployed config and restart the server to tune it without a build.
An existing config without `zones.entries` has no zones until the shipped entries are copied into it.

Each entry has `id`, `name` with `en` and `pl` strings, `minLevel`, `maxLevel`, `mode`, `shapes` and `areas`.
Levels use the character's persistent MMO scale from `progression`, including when engine-level synchronization
is disabled. `Gradient` also uses `origin: [x, y]` and positive `radius` in cells. Each entry may set `npcHealth`
and `npcDamage`, both positive. If omitted, both default by minimum level: 1-14 to 1.0, 15-20 to 1.1, 21-26 to
1.25, 27-34 to 1.4 and 35+ to 1.6. `PartyLowest` entries without a strength override default to 1.0.
Each shape is a polygon of `[x, y]` cell coordinates. Each cell is 4096 game units, with coordinates rounded down.
Edges and vertices are inclusive. Only Tamriel uses these polygons. Named exterior world spaces match through
`areas`, independently of coordinates. Write editor names such as `CheydinhalWorld`, not the SDK's `L_` prefix.
The first matching entry wins, including where shapes share a border. An empty shapes/areas list matches nothing.
`interiors` lists interior cells by form id in hex, for example `00096AC5`. The SDK names such a cell's area
`L_PersistentDungeon-<form id>`, so a listed interior matches wherever the player enters it. Interiors have no grid,
so `Fixed` hashes the form id as `x` with `y = 0`, and an entry with interiors cannot use `Gradient`.

| Mode | Cell level |
|---|---|
| `Fixed` | A stable coordinate hash selects an integer from `minLevel` through `maxLevel`, inclusive, independently of players. The hash uses unsigned 32-bit `x * 73856093 XOR y * 19349663`, modulo the range width. |
| `Gradient` | `minLevel + round((maxLevel - minLevel) * clamp(distance(cell, origin) / radius, 0, 1))`. Distance is Euclidean in cells, rounding halves away from zero. |
| `ScalesAbove` | The greater of `minLevel` and the lowest expanded party level. `maxLevel` is ignored. |
| `PartyLowest` | The lowest expanded party level. Both range settings are ignored. |

The expanded party level starts with players standing in the exact same area and cell now. Each expands to all
online members of their party, including members in other cells. A solo player counts as one. Invitations do not
count. Only playing characters with loaded progression count. The lowest persistent MMO level wins. Without any
available character level the cell uses 1. Every level is at least 1. The SDK's engine level field limits spawn
levels to 65535. A named world space uses the cell coordinates reported there, including for a gradient.

Shipped entries use the game regions below, with the existing level ranges, origins, radii and strengths.
MMO bands map to engine levels through
`ceil(cellLevel / 2)`. They describe the roll level, not every creature's actual level from a leveled list.

| Id | MMO levels | Mode | Origin, radius | Catalog regions |
|---|---|---|---|---|
| `imperial-city` | 1-4 | Fixed | | CityIsleRegion |
| `blue-road` | 1-6 | Gradient | [27,22], 14 | Existing corridor |
| `imperial-sewers` | 10-15 | Fixed | | 21 interior cells, no Tamriel shape |
| `cheydinhal` | 1-14 | Gradient | [28,22], 16 | NibenayBasinValleySubRegion01-03, JerallValusMountainsSubRegion04-06 |
| `nibenay` | 15-20 | Gradient | [26,-4], 28 | NibenayBasinValleySubRegion04-06 |
| `heartlands` | 1-6 | Gradient | [4,15], 12 | Heartlands01 |
| `blackwood` | 21-26 | Gradient | [16,-15], 20 | BlackwoodSubRegion01-04 |
| `jerall` | 21-26 | Gradient | [6,29], 20 | JerallValusMountainsSubRegion01-03 |
| `great-forest` | 21-26 | Gradient | [0,15], 25 | GreatForestSubRegion01-06 |
| `colovian-highlands` | 27-34 | Gradient | [-17,23], 25 | ColovianHighlandsSubRegion01-04 |
| `west-weald` | 27-34 | Gradient | [-3,0], 22 | WestWealdSubRegion01-03, WestWealdSubRegion03A, WestWealdSubRegion03B |
| `kvatch` | 27-34 | Gradient | [-30,-2], 12 | KvatchRegion |
| `gold-coast` | 35+ | ScalesAbove | | GoldCoastSubRegion01-03 |
| `oblivion` | party | PartyLowest | | Named exterior worldspaces only |

Heartlands has NPC health/damage 1.0 and Colovian Highlands 1.4. Their EN/PL names are in config.

v1.3 (Pete, 27 Nov 2026): the basin's southern subregion 04 moved from Cheydinhal to Nibenay, so the 15-20 band
has room after the start (448 cells, was 248). The gradient origin moved to the junction with the start zone, so
levels continue from Cheydinhal's 14 at the border. The sharp jumps from Heartlands to the Great Forest and from
Cheydinhal to the Jerall Mountains stay on purpose, as clear borders. The Imperial City sewers are a dungeon for
levels 10-15 under the safe city: every catalog interior cell with "Sewer" in its editor id that has references
and leads outside, including the prison sewers and the Thieves Guild Old Way, Lost Catacombs and Hall of Epochs.
Each section gets its own stable level. Their Tamriel exit markers stay in `imperial-city` and are no longer Zone 1
candidates.
Each catalog area's `tes` vertices become one shape, in source order, using `floor(coordinate / 4096)`.
Consecutive duplicate vertices, including a repeated closing vertex, are removed. Disconnected areas stay separate.
This quantizes the game's outlines to cells; it does not use the catalog's map pixels or Unreal coordinates.

Specific zones precede general ones. Imperial City and the Blue Road are first. Nibenay precedes Heartlands:
Bravil (16,-8) belongs to both outlines and keeps levels 15-20. KvatchRegion precedes Gold Coast, which also
contains Kvatch (-36,-4). Chorrol (-17,23) is actually in GreatForestSubRegion04, not the Colovian outlines,
so it keeps Great Forest. Colovian Highlands starts farther northwest, for example (-27,20). Its gradient origin
remains Chorrol as requested. The Valus mountains belong to Cheydinhal, so (29,31) now resolves there.
The Imperial City gate (4,15) is in CityIsleRegion; the western approach (0,15) is Heartlands.
No source region needed replacement. Other anchors remain Bruma in Jerall, Skingrad in West Weald, Anvil in
Gold Coast and Leyawiin in Blackwood.

After these 14 entries, all 10 old Tamriel entries remain in their old relative order with `-fallback` ids,
their old shapes, names, level policies and strengths. Fallbacks have no named areas: city worldspaces belong
to the primary entries. Regions always win over these rectangles. The identical Blue Road fallback and the
fully covered Imperial City fallback intentionally win zero cells. Every previously zoned cell stays zoned;
the generator also refuses an unzoned Tamriel marker of any named type, covering cities and settlements as well
as dungeon entrances. Unmarked wilderness beyond both the catalog regions and the old shapes can remain unzoned.
[zone-places.md](zone-places.md) gives exclusive cell counts and all 351 selected entrance markers for this export.

Existing city `areas` are retained, including the already configured Imperial City MQ16 variants and
TGTempleOfTheEmperorZero. The generator verifies them against the catalog and adds any missing real `IC*`
district worldspaces whose parent is Tamriel, excluding MQ16 variants from automatic additions.
Oblivion now matches the 11 worldspaces verified in `places.json.gz`: OblivionRD001-OblivionRD007,
MS13CheydinhalOblivionWorld, MQ10BrumaOblivionGate, OblivionMQKvatch and MQ14OblivionWorld.
These names exist in the catalog; actual runtime travel and SDK area naming still require a game test.
A party dungeon can use `PartyLowest` only if it has an exterior worldspace. Shivering Isles (`SEWorld`)
remain outside the shipped zones.

### Reproduce the catalog import

Python 3.10+ and its standard library are enough, matching the repository's existing Python asset tools.
Download the CC0 weather and places exports from the [catalog downloads](https://catalog.obmp.readym.io/downloads).
Keep the compressed files outside the repository. From the repository root:

```powershell
python tools/generate-zone-regions.py "D:\AI Projects\library\obmp-catalog\weather.json.gz" "D:\AI Projects\library\obmp-catalog\places.json.gz"
# Prints shapes JSON per zone without writing files.
python tools/generate-zone-regions.py "D:\AI Projects\library\obmp-catalog\weather.json.gz" "D:\AI Projects\library\obmp-catalog\places.json.gz" --write
# Updates only the shipped config's entries and docs/features/zone-places.md.
python tools/generate-zone-regions.py "D:\AI Projects\library\obmp-catalog\weather.json.gz" "D:\AI Projects\library\obmp-catalog\places.json.gz" --check
python -m unittest discover -s tools -p "test_generate_zone_regions.py"
```

Use `--write` on the shipped config, not a deployed custom config. It preserves the stored fallback shapes and
all existing level settings. Unknown regions/worldspaces, collapsed polygons, unsupported catalog schemas and
unzoned named Tamriel markers fail loudly. `--check` verifies entries and the generated document without writes.
The inventory records export timestamps and compressed-file SHA-256 hashes, and lists markers once in config order.
`Fixed` and `Gradient` levels match `ZoneLevels.ForCell`, including unsigned hash overflow and rounding halves away
from zero. `ScalesAbove` and `PartyLowest` show `party`. Six Zone 1 dungeon candidates are marked by lowest entrance
level, then distance to Cheydinhal (27,22): three solo candidates followed by three for 2-3 players. These are
planning candidates; other interiors are still unzoned and the list does not prove encounter difficulty.
Sewer exits are excluded from the candidates, and the sewer interiors are listed with their levels.

PL: Granice pochodzą z regionów gry w katalogu OB:MP (CC0), po przeliczeniu na komórki po 4096 jednostek.
Nowe strefy to Centralne Równiny (Heartlands, 1-6) i Wyżyna Colovia (27-34). Droga do Cheydinhal zachowuje korytarz.
Nibenay wygrywa z Heartlands przy Bravil, Kvatch z Gold Coast, a Chorrol pozostaje w Wielkiej Puszczy zgodnie z grą.
Od v1.3 południe Nibenay Basin należy do Nibenay (15-20), a kanały Imperial City to dungeon na poziom 10-15.
Dawne prostokąty z końcówką `-fallback` uzupełniają luki po regionach. Nazwy stref są dostępne po polsku i angielsku.
Generator czyta lokalne pliki `.gz`; bez opcji drukuje JSON, `--write` zapisuje config i listę miejsc,
a `--check` sprawdza zgodność. Lista miejsc pokazuje poziom komórki wejścia, nie wnętrza lochu.

## Spawning and combat

`ZoneResolver` is the one place that supplies the level for a cell now. `ZoneMap` and `ZoneLevels` hold the pure
lookup and level policies. No custom ECS component is added.

The SDK's `ServiceRegistry` sorts updating services by Before/After constraints first, then ascending priority,
then full type name. `CellRespawnSystem` has priority 100 and no constraints. `ZoneSpawns` runs at 99 every tick,
writing `LevelOffset = ceil(cellLevel / 2) - SessionScaling.CurrentLevel()`, or 0 outside zones. The session
level is on the engine scale. It writes even a spawned
list's offset because the SDK can reset and refill it during the same tick. The SDK rolls the leveled list at
session level plus offset. `ZoneSpawnCorrections` runs at 101 and sets matching NPC levels to their spawner's
actual rolled engine level, which also covers respawns. Origins include the area, cell and spawner reference, so the
same reference in another world space cannot acquire this level. A living creature keeps its spawn level when
the party changes. The current cell level used for combat can change with its occupants.

Only damage between a player and an NPC is adjusted, in `HealthChangedEvent`'s Filter stage. For positive
`gap = current NPC cell MMO level - player MMO level`, damage taken is multiplied by `min(maxTaken, 1 + takenPerLevel * gap)`;
damage dealt by `max(minDealt, 1 - dealtPerLevel * gap)`. PvP, NPC against NPC, healing, unzoned cells and nonpositive
gaps keep the original gap multiplier. Friendly-fire and other Validate cancellations still apply.

Strength then stacks with the gap adjustment. Damage to an NPC is divided by its effective health multiplier:
`entry.npcHealth * rankHealth * (1 + groupHealthPerPlayer * max(0, playersInNpcCell - 1))`. Damage from an NPC
to a player is multiplied by `entry.npcDamage * rankDamage`. The player count includes all online playing
characters in the exact NPC cell, regardless of party, and changes dynamically. NPC health itself is not rewritten.
Both adjustments currently require a zone. Nonpositive gaps can still receive strength adjustments.

Global `zones` strength settings are `eliteHealth: 3`, `eliteDamage: 1.5`, `bossHealth: 8`, `bossDamage: 2` and
`groupHealthPerPlayer: 0.6`. Rank ids come from the same `progression.elites` and `progression.bosses` lists used
for XP, with bosses taking precedence. A rare gets extra strength only if also listed as an elite or boss.

## Limits and ReadyM follow-up

- NPC inventory rolls and container loot still use the SDK's session or master level. The offset changes which
  creature is selected and the NPC level, without rerolling its inventory.
- Only interiors listed in `interiors` are zoned (v1.3: the Imperial City sewers). Mapping other dungeons through
  their entrance doors remains a follow-up, so other interior party dungeons do not yet get the party-lowest rule.
- The interior area name assumes the SDK's `L_PersistentDungeon-<cell form id>` uses the load-order form id of an
  `Oblivion.esm` cell, which equals the catalog's form id. This needs a game test in the sewers with `/azone`.
- A session level of 0 leaves the SDK's fill pass waiting. Zones do not change the session policy or force a spawn.
- The offset workaround is needed because the SDK's spawner roll is internal. ReadyM is adding supported cell
  level lookup. Move that integration into `ZoneResolver` when available, then retire the offset and correction
  passes. User-defined component serialization is currently broken, so a custom component is not a substitute.
- Spawn selection, client application of NPC levels, combat authority, native centre messages and world space
  names need an actual server and game test. Headless browser tests cannot display the game's native message.
