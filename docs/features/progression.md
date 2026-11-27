# MMO progression

Each character has a separate MMO level, starting at 1 and capped at 40 by default. Oblivion's skills still grow
through use and remain the source of power. XP and perk points do not directly increase stats. One MMO level is
half an engine level: `ceil(ourLevel / 2)`. Zones, party scaling and combat gaps use MMO levels.

The playtest targets are level 5 after about 30 minutes, 10 after two hours, 20 after seven hours and 40 after
20-25 hours. These are targets, not measured results. Tune the XP table and grants against level-up telemetry.

## Storage and lifecycle

`character_progression` is one row per character, deleted with the character. It stores level, XP into that level,
unspent perk points, the last level's reached-at timestamp, the gathering window and raw XP count, and skill peaks.
An absent row starts at level 1. The server loads through the ordered database writer after account/character
load, or after a new character starts playing. Every award writes an immutable snapshot on that writer, without
blocking the update thread. A reconnect reads after preceding writes, including the gathering window and peaks.

Awards require loaded progression and a playing character. Disabled progression grants no XP, hides the bar and
does not write engine levels. Existing progression is retained. If staff lower the configured max below a stored
level, loading clamps the level and clears current XP, with a warning. It retains existing perk points.

## Configuration

Edit `progression` in the deployed `server/config.json`, then restart. No build is needed. The shipped file is
`src/CyrodiilRP.Server/config.json`. Startup refuses invalid ranges, nonfinite or negative multipliers, malformed
hex form ids, nonpositive XP costs, or a table whose length differs from `maxLevel - 1`.

| Setting | Default and meaning |
|---|---|
| `enabled`, `maxLevel` | `true`, `40` |
| `xpToNext` | 39 costs, indexed by level minus one, listed below |
| `killXp` | 100 for a normal enemy at the killer's level |
| `higherPerLevel`, `higherMax` | 0.05 per level above the killer, capped at 0.25 extra |
| `lowerPerLevel`, `greyAt` | 0.10 less per level below, zero at ten below |
| `eliteMult`, `bossMult`, `rareMult` | XP multipliers 3, 10, 15 |
| `elites`, `bosses`, `rares` | Empty lists of base form ids in hex, optionally prefixed by `0x` |
| `partyRangeCells`, `groupBonusPerMember` | 2 cells, 0.10 extra pool per extra eligible member |
| `majorSkillXp`, `minorSkillXp` | 30 or 10 per whole skill point |
| `fishingXp`, `rareFishMult` | 20 per fish, multiplied by 3 for a rare species |
| `gatheringXp` | 15 per supported Jobs action |
| `gatheringHourlySoft` | 1500 raw XP from fishing, gathering and crafting before halving |
| `perkPointsPerLevel` | 1 unspent point per level gained |
| `syncEngineLevel` | `true`, experimental engine-level writes |

```json
[800, 900, 1000, 1100, 2500, 2600, 2700, 2800, 2900, 3000, 3100, 3200, 3400,
 4000, 4200, 4400, 4600, 4800, 5000, 5600, 5800, 6000, 6200, 6400, 6600, 6800,
 6900, 7000, 7100, 7200, 7300, 7400, 7500, 8200, 8500, 8800, 9100, 9400, 9700]
```

## XP flow

Kills use `HealthChangedEvent` from `OblivionMP.SDK.Common.Events`. `Stage.Filter` records whether the NPC was
alive before application. `Stage.Observe` runs after the SDK's Apply handler has written `Npc.Hp`, so health zero
confirms death. Only accepted damage starts TTK. The first accepted player damage starts a monotonic clock.
The entity-handle registry awards at most once per lifetime and resets on an observed living NPC after revival.
Gone handles are removed every 30 seconds. The SDK's `OblivionMpCSharpMod.GameEvents.CharacterDiedEvent` is a
client interop event and is not used. These mechanics were checked in the supplied decompiled SDK, not in game.

The killing blow must come from an online playing `MainCharacter`. PvP victims, summons (`SummonedBy` mixin),
and NPCs with level zero give no kill XP. The enemy's MMO level is the current zone cell level, or `Npc.Level * 2`
outside zones. The killer's MMO level determines the gap. Rank precedence for XP is rare, boss, elite, normal.
For strength, a boss takes precedence over an elite independently, so a rare also listed as a boss has boss strength.

Party recipients must be online, playing, have loaded progression, and stand in the NPC's area within the circular
cell range. The killer always receives a share, including a solo kill beyond the sharing range. Interior cells have
no coordinates, so everyone in the same interior is in range. Invitations never count. The pool is multiplied by
`1 + groupBonusPerMember * (members - 1)`,
rounded once to the nearest integer with halves away from zero, then split equally. Remaining single points go
by player number. A capped member still counts in the split but receives no XP.

Skill increases have no dedicated server event in this SDK. A named one-second timer reads
`MainCharacter.TryGetBaseSkillAttributeValue` from `BaseSkillAttributes`, never effect-modified `SkillAttributes`.
Two complete equal samples establish a new character's initial baseline without XP. Whole points above persisted
peaks award XP. Decreases, fractions and regaining a previously reached value do not. This prevents repeated awards
through reconnects, drains, cap reductions or staff resets. Staff increases can award skill XP.
Chosen majors use `SpecializationRules.FindMajorSkills`. **SDK limit:** unchosen caps leave the game's original
class alone, but the server sees only its form id and cannot read its majors, including a custom class. Until a
specialization is chosen, all skill increases use the minor rate and a per-session warning records the limitation.
An authoritative platform hook for original class majors is required to complete that part of the design.

`FishingSkills.RecordCatch` grants XP only for landed fish, using the species' `Rare` flag. Finds and failed catches
do not grant progression XP. Existing server-authoritative Jobs events `OreMinedEvent`, `StoneQuarriedEvent` and
`TreeFelledEvent` grant gathering XP in Observe. They reward the event's finishing actor once per successful action,
not each hit or each helper. Herbalism has no equivalent clean success event. Its Validate handler can cancel the
normal inventory transfer while successfully giving an unknown herb, so that transfer is not a complete hook.

Fishing, gathering and crafting share one persisted real-time hour, starting at the stored window timestamp.
After an hour elapses, the next gathering grant starts a new window. Crossing the threshold gives full XP for the
remaining allowance and half for the excess. Later grants in that window give half, rounded down per action.
The counter tracks raw pre-softening XP. Reconnecting does not reset it. Kills, skills, quests, dungeon completion,
discovery and staff grants bypass the soft cap.

Each level consumes its cost and carries the excess. One grant can gain several levels. Every level gives the
configured perk points. At max level XP is cleared and further XP stops. A native centre message shows the final
level reached in the reader's language, through the same `CenterMessage` RPC used by zones.

## HUD, commands and API

The read-only `ui/progression` HUD shows level, current/next XP and unspent perk points. At the cap it shows
Maximum level. It follows PartyOverlay's ready/reload, locale and pawn visibility flow, takes no input and sends
state only when displayed values change. Its position and interaction with the native HUD need an in-game check.

`/xp` reports level, XP and unspent points. `/axp give <player-number> <xp>` grants positive XP;
`/axp set <player-number> <level>` resets current XP and sets unspent points to `(level - 1) * perkPointsPerLevel`
for repeatable tests. Both require duty and permission `axp`, under the usual owner exception, and are audited.
The CharacterProgression migration grants `axp` to administrators without replacing custom permissions. Run the
migrator before starting this build. Spending points is not implemented yet.

Other server mods can depend on CyrodiilRP and inject `CyrodiilRP.Server.Progression.IProgression`.
`Grant(PlayerId player, ProgressionSource source, int xp)` is server update-thread only, never a client RPC.
It returns false while disabled, not playing or not loaded, and true for an accepted grant, including at the cap.
Callers own success validation and once-only delivery. Use `Gathering` or `Crafting` for the shared soft cap,
and `Quest`, `Dungeon` or `Discovery` for future awards. There is no automatic quest/discovery grant.

## Engine-level experiment and telemetry

With `syncEngineLevel`, joining/starting play and each level-up write
`MainCharacter.Level = ceil(ourLevel / 2)` once a main character with initialized vitals exists. A pending write
is retried every second until then. Every write logs character, old engine level, MMO level and new engine level,
with `unverified=true`. The game might ignore it or later replace it through its sleep level-up. This has not
been verified in a live server/game session. No engine skill, attribute, experience or sleep behavior is changed.

Each earned level emits a structured `progression.level` server log with character ref, level, source and played
seconds, including this session's seconds since the last playtime flush. A multi-level grant emits every level.
The row keeps the last reached timestamp. A death after accepted player damage logs `progression.kill` with NPC
reference/base, TTK in milliseconds, zone id (null outside zones) and enemy MMO level. One-hit kills have TTK zero.

Follow-ups: perk spending, quests/dungeons/discovery callers, herbalism success wiring, original-class majors,
content lists for elites/bosses/rares, nameplate level, measured pacing and the engine-level experiment.
Live tests must also cover death replication, simultaneous hits, summons, cancelled damage, party range,
skill snapshot timing and Jobs integration. Unit tests do not confirm platform behavior in game.
