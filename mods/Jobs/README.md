# Jobs

Roleplay jobs for the ReadyM Roleplay server on OblivionMP, from the game's own assets: no new model.

- **Woodcutting:** hit a tree with a battle axe's power attack. There is no key: the game's own power attack is the
  swing, in either view. Every hit that lands counts, from anyone; the tree falls and sinks into the ground for
  everyone, a stump stays, and the tree grows back after a while. The wood is shared by everyone who hit it, as ore
  is. A server can keep felling to lumber camps (off by default: any tree anywhere).
- **Mining:** hit an ore node with a warhammer's power attack. There is no key: the game's own power attack is the
  swing, in either view. Every hit that lands counts, from anyone, a better hammer needs fewer, and each wears it a
  little. What a spent node gives is shared by everyone who hit it, by their hits, most valuable first: whoever did
  most of the work gets the gems and nuggets, and one last hit snuck in gets a stone brick or nothing. The node bursts
  into pieces when it is spent, giving nuggets, stone bricks and now and then a gem, and grows back after a while.
- **Quarrying:** the same on rock inside a quarry, which gives stone bricks every few hits and does not run out.
  Quarries are where a server says: around every map marker named "...Quarry..." in its plugins, or in its config.
  Rock outside a quarry is left alone, and the first power attack on it says where stone can be had.

The server decides everything. A felled tree, a spent node and a block of quarried stone are networked events
(`TreeFelledEvent`, `OreMinedEvent`, `StoneQuarriedEvent`), so other mods can refuse or react to them.

> **Needs SDK features no package has yet.** It builds against an oblivion-mod checkout on its rp-framework branch
> (see [../README.md](../README.md)), which carries new SDK APIs: `IFoliage` and `IProps` (client and server),
> `IAnimations`, `IWeaponHits`, `IEquippedWeapon` and `ILocalProps` (client). The relay, the game's OblivionMp mod and
> the SDK mod must be that build too. What changed in the SDK and why: [docs/SDK-CHANGES.md](docs/SDK-CHANGES.md).

## How trees work in Oblivion Remastered

A vanilla tree is not a reference the game can disable. Its TREE form carries a `UFoliageType`, and the editor import
turned every placed tree into an instance of a foliage mesh baked into the map. Disabling the TES reference under it
changes nothing on screen. So the SDK hides the instance itself: it removes it from its foliage component, collision
and all, on every client, and removes it again whenever that part of the world streams back in.

An instance is named by its mesh and its location in whole centimeters (`FoliageId`). That is the same on every
client and across restarts, so the server can save which trees are down.

Trees that mods add as plugin references (an `.esp` with a SyncMap through TesSyncMapInjector) are a different case,
real references, and are not handled yet.

## In game

- **A battle axe's power attack on a tree:** every one that lands on the trunk counts ("Oak: 1 / 4", ...), anyone's,
  and the tree comes down on the last hit needed, "Timber!" and the drops. A thicker trunk takes more hits (4 for an
  average oak, up to 10), and so does a hard wood with a weak axe: every tier the axe falls short doubles them (an
  iron axe on oak takes four times as many), every tier above saves 15%. Woods by hardness: pine, hemlock, redwood,
  poplar and aspen 1, birch and holly 2, oak, maple and yew 3; axes by material from iron 1 to daedric 5. A bigger
  tree gives more wood: with the square of its size by default, so a tree twice the size gives four times as much,
  times its kind's wood (a birch gives less than an oak). Every drop is its own entry, so leaves, nuts or resin can
  come with the wood, by chance, from some kinds only. Every counted hit wears the axe down a little (2 durability by
  default); a hammer repairs it.
- **Sharing:** a tree's hits add up for everyone who strikes it, and what it gives is shared by those hits, most
  valuable first, as an ore node's is: whoever did most of the work gets the best of it, and one last hit snuck in
  gets little or nothing. Gnarl Bark is worth 1 gold, so with bark alone the share is simply by hits.
- **Lumber camps** (`lumberCampsOnly`, off by default): a tree can only be felled within 30 m of a lumber camp, a map
  marker named "...Lumber..." or one in the config. The first power attack on a tree outside every camp says where
  wood can be had.
- The animation is the battle axe's own power attack. The warhammer's suits chopping better, but a weapon's power
  attack is the game's to choose: playing the warhammer's on a battle axe would mean replacing the game's attack
  choice natively, for every battle axe in combat too, so it stays the battle axe's.
- Messages in the top left say why a hit did not count: no axe, too far, outside a lumber camp, someone felled the
  tree a moment before.

## For other mods

A mod that draws its own woodcutting interface (a CEF page, a widget) injects `IWoodcutting` from
`Roleplay.Jobs.Client` and lists `Roleplay.Jobs` among its manifest's dependencies. Read only: the server decides.

| Member | What |
|---|---|
| `ShowMessages` | false hands the top-left messages over to the mod |
| `LumberCampsOnly`, `LumberCamps` | whether trees are kept to lumber camps, and the camps (name, center, radius) |
| `HitCounted` | a hit of this player's the server counted: the tree's `FoliageId`, `Kind` ("Oak", empty for an unlisted kind), `Hits` so far (everyone's), `HitsNeeded` |
| `ChopEnded` | `Felled` (a tree this player hit came down; the message lists their share) or `Refused`, and the message Jobs would show |
| `TreeFelled` | any tree coming down near this player, theirs included (`ByLocalPlayer`), with its kind, scale and `Feller` |
| `TreeGrewBack` | a felled tree's `FoliageId` |

A tree has no ref id: vanilla trees are foliage instances, not game references, so a tree is its `FoliageId` (mesh and
position in whole centimeters), the same on every client and the server. Events come on the game thread; one that
throws is logged and does not stop woodcutting.

**Networked events.** The server raises these as the player whose hit did it, Area scope, server authoritative (a
client cannot raise them), declared in `Roleplay.Jobs.Common`:

| Event | When | Fields |
|---|---|---|
| `TreeFelledEvent` | a tree comes down | `Feller`, the tree (`Mesh`, `X`, `Y`, `Z`, or `Tree()`), `Kind`, `Scale`, `FallYaw` |
| `OreMinedEvent` | an ore node is spent | `Miner`, `Node` (its id), `Kind`, `Position`, `Scale` |
| `StoneQuarriedEvent` | a block comes loose in a quarry | `Quarrier`, `Quarry` (its name), `Position` |

A server mod refuses one in `Stage.Validate` (`context.Cancel(HarvestRefusal.Other)`: a protected grove, a quest
tree) and reacts to one in `Stage.Observe` on any machine (experience, a contract, a log). Jobs' own `Validate` refuses
with `HarvestRefusal.Gone` when the tree or node is already down. A refusal reaches `Stage.Rejected` on the server and
on the player's client, and Jobs tells the player why. Jobs does its work in `Stage.Apply`: on the server it takes the
tree or node out and shares or gives the yield, on the clients near it plays the fall or the burst.

## Server owners

1. `.\MakeModFolder.ps1 Release`
2. Copy `Output/mods/Roleplay.Jobs/` into the server's `mods/` folder.
3. Tune `mods/Roleplay.Jobs/server/config.json`: which meshes are trees, reach, hits, the axes, their tiers, the
   drops, their values and how each grows with a tree's size, lumber camps, how long a tree takes to grow back, how a
   felled tree falls (`treesFall` and the `fall*` keys), and the kinds of tree: each one's stump width, wood, trunk
   thickness next to an oak's and hardness tier; and for mining, the warhammers and their tiers, the ore kinds and
   their drops, the quarries and their drops. Every key has its default and a comment.

**Quarries from map markers.** Place a map marker in your plugin, in Tamriel, and name it with "Quarry" in it, such
as "Great Forest Quarry", over a cliff face, and ship the plugin as a mod's client content (list it in the mod's
`clientContentFiles`, as Jobs does with `QuarryJob.esp`): players get it and their game loads it, so they see the
marker on their map. At start the server reads every `.esp` and `.esm` in the mods' folders and in its modloader's
`@GAME/Content/Dev/ObvData/Data`, and every such marker becomes a quarry 20 m round (`quarryRadius`).
`quarriesFromMarkers`, `quarryMarkerName`, and at the top `markerWorldspaces` and `pluginFolders`, change how;
`quarries` adds ones without a marker. The server log lists every quarry it found (`Jobs: quarry ...`).

**Lumber camps from map markers.** The same, with "Lumber" in the marker's name ("Pell's Gate Lumber Camp"), but only
once `woodcutting.lumberCampsOnly` is on: off, any tree anywhere can be felled. A camp is 30 m round
(`lumberCampRadius`); `lumberCampsFromMarkers`, `lumberCampMarkerName` and `lumberCamps` work as the quarries' keys do.
The server log lists every camp (`Jobs: lumber camp ...`), and warns when camps are required but there is none.

Felled trees are saved to `server/felled-trees.json` on every change, with the time each grows back, so a restart
does not regrow the forest.

**The reward is a stand-in.** Oblivion has no log or firewood item. Until the mod ships one (a plugin plus a SyncMap
entry), a felled tree gives Gnarl Bark; `drops` takes any form id.

## Not done yet

- **Woodcutting by power attack is untested in game**, as are lumber camps and the three networked events. The
  felling, the fall and the stump were seen working with the old G key.
- The kinds' wood, girth, stump width and tiers, the axe tiers and the tier penalty are guesses: untested in game.
- `tools/TreeProbe` is a UE4SS Lua mod for single player (a plain launch: under the ReadyM launcher UE4SS loads mods
  from `%APPDATA%\ReadyM.Launcher\Oblivion\Mods` instead). Copy it to the game's `Binaries/Win64/Mods/`, then:
  Numpad 1 lists the instanced meshes around the player (class, mesh path, distance) to `UE4SS.log`; 2 removes the
  nearest tree the way the SDK does; 3 puts it back; 4 and 5 play the chop and mine montages in both views.
- `tools/HitBoxProbe` is a UE4SS Lua mod for finding out why a standing battle axe power attack often makes no impact
  on a tree. It works in multiplayer too, from `%APPDATA%\ReadyM.Launcher\Oblivion\Mods\HitBoxProbe`. Numpad 6 draws
  every weapon hit box near the player (red while it can hit) and logs the player's own to `UE4SS.log` whenever it
  turns on or off: where it is, how far from the player, and what it already overlaps.
- Trees far away are drawn by the map's distant LOD (the foliage HLOD layer), which this does not touch because
  editing it costs too much: a felled tree may still show on the horizon.
- A felled tree leaves a stump (Stump01, the game's chopping block, sunk into the ground, turned at random and sized
  to the tree) until it grows back. It leaves no log: the falling tree takes its place, and logs left lying would
  clutter the world.
- A felled tree falls (`treesFall`, on by default) for every player within 150 m: their client puts a copy of the tree
  in its place, with no collision, tips it away from the chopper, faster as it goes, to 85 degrees over 2.5 s (longer
  for a bigger tree), leaves it lying 2 s, then sinks it 15 m into the ground over 6 s and removes it. It is the
  tree's own mesh, so no tree needs a model of its own. On a slope it can cut into the hill, and nothing breaks its
  fall. A player who comes close later, or joins later, sees only the stump. Untested in game.
- A tree is a foliage mesh whose name (not its folder) holds "tree" and not "branch", "bark", "shrub", "bush" or
  "stump": the fallen-branch meshes (`SM_Branches_TreeBark_*`) near Weye counted as trees before.
- Firewood on a chopping block.
- **Mining by power attack is untested in game.** Before it, seen working: iron (KotN's vein, whose mesh comes from
  its blueprint) and madness nodes, warhammer hits on them, the drops, the flying bricks. Not yet:
  - power attacks counting at all, and the line traced from the character that tells what the hammer struck;
  - quarries: the markers read from a plugin, the hits on cliff faces and rock inside one, the hint outside;
  - the burst of a spent node, and chips lying at the miner's feet instead of falling through the ground;
  - the Rich node as the remastered `SM_goldrock02`, the tiers and the wear.
- Meadow rocks were mined one by one with G for a while: picking the rock in front from bounding boxes went wrong too
  often, and the team's servers keep harvesting to marked places anyway. Quarries replaced them.
- A quarry is a circle around its marker, so it can reach rock on the far side of a hill; a server wanting less sets
  `quarryRadius`. A quarry does not run out.
- No ore node ships: a server places its own, in `mining.nodes` or in game with `allowNodePlacement` (J places one of
  the kind Shift+J picked, or removes the placed one in front).
- Experimental, off by default (`experimentalChips`): every counted hit throws stone bricks off where it landed, to
  lie a moment at the miner's feet.

## Layout

| Path | What |
|---|---|
| `Roleplay.Jobs.Common/` | the RPC contracts, the networked events (`JobEvents.cs`), and `TreeFilter`, which both halves use to agree on what a tree is |
| `Roleplay.Jobs.Server/` | `WoodcuttingService` and `MiningService` (the rules), `YieldShare` (who gets what), `JobAreas` (quarries and lumber camps), `FelledTreeStore` and `OreNodeStore` (the save files), `JobsConfig` |
| `Roleplay.Jobs.Client/` | `WoodcuttingClient` and `MiningClient`: the power attacks; `TreeFallService`: the fall |
| `Roleplay.Jobs.Tests/` | xUnit tests for the filters, the yields and shares, the areas, the save files and the shipped config |
| `Content/config.json` | the server config shipped with the mod |
| `tools/TreeProbe/` | the in-game probe above |

## Develop

```powershell
dotnet test Roleplay.Jobs.sln
.\MakeModFolder.ps1 Debug     # with .pdb files
```
