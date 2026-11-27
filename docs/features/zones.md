# Level zones v1

The prototype divides Tamriel into level zones, with Cheydinhal as the starting region and harder enemies farther
away. The draft borders follow the cell grid, not the landscape. Staff should tune them in game with `/azone`.
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
Each shape is a polygon of `[x, y]` cell coordinates. Each cell is 4096 game units, with coordinates rounded down.
Edges and vertices are inclusive. Only Tamriel uses these polygons. Named exterior world spaces match through
`areas`, independently of coordinates. Write editor names such as `CheydinhalWorld`, not the SDK's `L_` prefix.
The first matching entry wins, including where shapes share a border. An empty shapes/areas list matches nothing.

| Mode | Cell level |
|---|---|
| `Fixed` | A stable coordinate hash selects an integer from `minLevel` through `maxLevel`, inclusive, independently of players. The hash uses unsigned 32-bit `x * 73856093 XOR y * 19349663`, modulo the range width. |
| `ScalesAbove` | The greater of `minLevel` and the lowest expanded party level. `maxLevel` is ignored. |
| `PartyLowest` | The lowest expanded party level. Both range settings are ignored. |

The expanded party level starts with players standing in the exact same area and cell now. Each expands to all
online members of their party, including members in other cells. A solo player counts as one. Invitations do not
count. The lowest `MainCharacter.Level` wins. Without any available character level the cell uses 1. Every level
is at least 1. The SDK's level field limits spawn levels to 65535.

Shipped order: Imperial City 1-5, Road to Cheydinhal 1-5, Cheydinhal 1-10, Jerall Mountains 15-25, Great Forest 20-25,
West Weald 25-30, Kvatch 30-40, Gold Coast 40+, Nibenay Valley 10-15, Blackwood 15-20, Shivering Isles 40+, Oblivion
gates at party level. Gold Coast and Shivering Isles use `ScalesAbove`; Oblivion uses `PartyLowest`.
The Shivering Isles world space names were not found in the repo or SDK, so their `areas` list is empty, as is
Oblivion's pending verification in game. Those two entries currently match nothing. Add verified exterior world
space names to enable them. A party dungeon can use a `PartyLowest` entry only if it has an exterior world space.

## Spawning and combat

`ZoneResolver` is the one place that supplies the level for a cell now. `ZoneMap` and `ZoneLevels` hold the pure
lookup and level policies. No custom ECS component is added in v1.

The SDK's `ServiceRegistry` sorts updating services by Before/After constraints first, then ascending priority,
then full type name. `CellRespawnSystem` has priority 100 and no constraints. `ZoneSpawns` runs at 99 every tick,
writing `LevelOffset = cellLevel - SessionScaling.CurrentLevel()`, or 0 outside zones. It writes even a spawned
list's offset because the SDK can reset and refill it during the same tick. The SDK rolls the leveled list at
session level plus offset. `ZoneSpawnCorrections` runs at 101 and sets matching NPC levels to their spawner's
actual rolled level, which also covers respawns. Origins include the area, cell and spawner reference, so the
same reference in another world space cannot acquire this level. A living creature keeps its spawn level when
the party changes. The current cell level used for combat can change with its occupants.

Only damage between a player and an NPC is adjusted, in `HealthChangedEvent`'s Filter stage. For positive
`gap = current NPC cell level - player level`, damage taken is multiplied by `min(maxTaken, 1 + takenPerLevel * gap)`;
damage dealt by `max(minDealt, 1 - dealtPerLevel * gap)`. PvP, NPC against NPC, healing, unzoned cells and nonpositive
gaps are unchanged. Friendly-fire and other Validate cancellations still apply.

## Limits and ReadyM follow-up

- NPC inventory rolls and container loot still use the SDK's session or master level. The offset changes which
  creature is selected and the NPC level, without rerolling its inventory.
- Interiors, including `L_PersistentDungeon-*`, are not zoned in v1. Mapping a dungeon through its entrance door is
  v1.1. Consequently interior party dungeons do not yet receive the party-lowest rule.
- A session level of 0 leaves the SDK's fill pass waiting. Zones do not change the session policy or force a spawn.
- The offset workaround is needed because the SDK's spawner roll is internal. ReadyM is adding supported cell
  level lookup. Move that integration into `ZoneResolver` when available, then retire the offset and correction
  passes. User-defined component serialization is currently broken, so a custom component is not a substitute.
- Spawn selection, client application of NPC levels, combat authority, native centre messages and world space
  names need an actual server and game test. Headless browser tests cannot display the game's native message.
