# Jobs

Roleplay jobs for the ReadyM Roleplay server on OblivionMP, from the game's own assets: no new model.

- **Woodcutting:** hit a tree with a battle axe. There is no key: the game's own attacks are the swings, in either
  view. A tree has health: a normal attack does a little damage and always lands, a power attack does five times as
  much. Every hit that lands counts, from anyone; the tree falls and sinks into the ground for everyone, a stump stays,
  and the tree grows back after a while. The wood is shared by everyone who hit it, as ore
  is. A server can keep felling to lumber camps (off by default: any tree anywhere).
- **Mining:** hit an ore node with a warhammer. There is no key: the game's own attacks are the swings, in either
  view. A node has health, as a tree does: a normal attack always does 1, a power attack 5, more with Strength (+2 for
  every 10 above 40, and +2 for every tier the hammer is above the ore). Every hit that lands counts, from anyone, and
  each wears the hammer by its damage. A worn warhammer glances off and a broken one does nothing, as a worn axe. What a spent node gives is shared by everyone who hit it, by their damage, most valuable first: whoever did
  most of the work gets the gems and nuggets, and one last hit snuck in gets a stone brick or nothing. The node bursts
  into pieces when it is spent, giving nuggets, stone bricks and now and then a gem, and grows back after a while.
- **Quarrying:** the same on rock inside a quarry, which gives stone bricks every 15 damage (three power attacks or
  fifteen normal ones) and does not run out.
  Quarries are where a server says: around every map marker named "...Quarry..." in its plugins, or in its config.
  Rock outside a quarry is left alone, and the first attack on it says where stone can be had.
- **Carrying:** U picks up the barrel, crate, sack or log in front of you and puts down the one in hand. It hangs on
  your right hand for everyone near, with your arms held out (the game's hand-to-hand idle, on the upper body only,
  played again before it ends), and sits one way while you stand and another while you move. It weighs its weight on
  you as a Burden (a log 300, so you barely move). You put it down where you stand when you die or leave, and what lies
  in the world stays over restarts (`carryables.json`). Put down, it is swept from in front of your chest to its spot
  and let fall onto whatever is below, the ground or another carryable, so barrels stack; with something in the way, or
  nothing to land on, it stays in your hand. Dropped by dying or leaving, it lands at your feet without that check. Each kind is of a category (Beam, Box, Barrel, Sack) and takes
  its weight and how it hangs in the hand from it, unless the kind gives its own, so a new plank or pole is one line
  with `"category": "Beam"`.

The server decides everything. A felled tree, a spent node and a block of quarried stone are networked events
(`TreeFelledEvent`, `OreMinedEvent`, `StoneQuarriedEvent`), so other mods can refuse or react to them.

> **Needs SDK features no package has yet.** It builds against an oblivion-mod checkout on its rp-framework branch
> (see [../README.md](../README.md)), which carries new SDK APIs: `IFoliage` and `IProps` (client and server),
> `IAnimations`, `IWeaponHits`, `IEquippedWeapon` and `ILocalProps` (client), with `ILocalProps.FindRest` for carrying,
> and `ICustomItems` (server) for the firewood and the lumber axe. The relay, the game's OblivionMp mod and
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

- **A battle axe's attack on a tree:** every one that lands on the trunk does damage ("Oak: 5 / 20", ...), anyone's:
  1 for a normal attack, 5 for a power attack, more with Strength above 40: a normal attack +1 for every 20, a power
  attack +2 for every 10 (at Strength 45, 1 and 5; at 60, 2 and 9; at 100, 4 and 17). The tree comes down when it has taken its health, "Timber!" and the
  drops. A thicker trunk has more health (20 for an average oak, four power attacks or twenty normal ones, up to 50),
  and so does a hard wood with a weak axe: every tier the axe falls short doubles it (an iron axe on oak needs four
  times the damage). An axe above the wood hits harder instead, +1 a normal attack and +2 a power attack for every
  tier above (an ebony axe on pine: 4 and 11 at Strength 40), and the tree's health stays the same, so the panel's
  numbers always mean the same. Woods by hardness: pine, hemlock, redwood,
  poplar and aspen 1, birch and holly 2, oak, maple and yew 3; axes by material from iron 1 to daedric 5. A bigger
  tree gives more wood: with the square of its size by default, so a tree twice the size gives four times as much,
  times its kind's wood (a birch gives less than an oak). Every drop is its own entry, so leaves, nuts or resin can
  come with the wood, by chance, from some kinds only. Every counted hit wears the axe by its damage (0.4 durability a
  point by default: 2 for a power attack); a hammer repairs it. A worn axe bites less: below half its durability a
  hit may glance off and do nothing ("The blade glances off."), from no chance at half to one in two just before it
  breaks (one in four at a quarter), and a broken one does nothing at all ("Your axe is too dull to bite."). Every counted hit throws a few pieces of bark (the
  Gnarl Bark ingredient's own model) off the trunk, back towards the chopper, for everyone near (`chipsPerHit`).
- **Why normal attacks:** a standing power attack with a battle axe often makes no impact on a trunk at all. The game's
  hit box on the blade turns on for the swing but registers nothing, until some hit lands (a crouched one did); after
  that every power attack connects. Normal attacks always land, so they are the way, and a power attack is the bonus
  when it lands. `tools/HitBoxProbe` shows the hit box's state.
- **Sharing:** a tree's damage adds up for everyone who strikes it, and what it gives is shared by that damage, most
  valuable first, as an ore node's is: whoever did most of the work gets the best of it, and one last hit snuck in
  gets little or nothing. Firewood is worth 1 gold, so with it alone the share is simply by damage.
- **Lumber camps** (`lumberCampsOnly`, off by default): a tree can only be felled within 30 m of a lumber camp, a map
  marker named "...Lumber..." or one in the config. The first attack on a tree outside every camp says where wood can
  be had.
- **The panel:** a small half-transparent plate at the top right, 30% down the screen, shows the tree, vein or quarry
  last hit ("Oak", "Iron vein", "Aleswell Quarry"), its progress ("12 / 20" and a thin bar), and under it what came of
  it: what the player got ("+3 Firewood"), a hint, or why a hit did not count (no axe, too far, outside a lumber
  camp, someone felled it a moment before). It fades six seconds after the last hit and steps aside while a game menu
  is open. Its page is `Content/web/panel.html` (plain HTML, Cinzel and Alegreya, the fonts' OFL licences beside
  them), hosted by `JobsPanel`; opened in a browser it shows a preview with a mock game (C, P, M, R and Q).

## For other mods

A mod that draws its own woodcutting interface (a CEF page, a widget) injects `IWoodcutting` from
`Roleplay.Jobs.Client` and lists `Roleplay.Jobs` among its manifest's dependencies. Read only: the server decides.

| Member | What |
|---|---|
| `ShowMessages` | false keeps woodcutting out of Jobs' own panel, for a mod that draws its own |
| `LumberCampsOnly`, `LumberCamps` | whether trees are kept to lumber camps, and the camps (name, center, radius) |
| `HitCounted` | a hit of this player's the server counted: the tree's `FoliageId`, `Kind` ("Oak", empty for an unlisted kind), `Damage` so far (everyone's), `Health` |
| `ChopEnded` | `Felled` (a tree this player hit came down) or `Refused`, the sentence Jobs has for it, and `Gains`, what the player got, each such as "3 Firewood" |
| `TreeFelled` | any tree coming down near this player, theirs included (`ByLocalPlayer`), with its kind, scale and `Feller` |
| `TreeGrewBack` | a felled tree's `FoliageId` |

A tree has no ref id: vanilla trees are foliage instances, not game references, so a tree is its `FoliageId` (mesh and
position in whole centimeters), the same on every client and the server. Events come on the game thread; one that
throws is logged and does not stop woodcutting.

**Networked events.** The server raises these as the player whose hit did it, Area scope, server authoritative (a
client cannot raise them), declared in `Roleplay.Jobs.Common`:

| Event | When | Fields |
|---|---|---|
| `TreeFelledEvent` | a tree comes down | `Feller` (the character; `JobEvents.PlayerOf` gives its player), the tree (`Mesh`, `X`, `Y`, `Z`, or `Tree()`), `Kind`, `Scale`, `FallYaw` |
| `OreMinedEvent` | an ore node is spent | `Miner` (the character), `Node` (its id), `Kind`, `Position`, `Scale` |
| `StoneQuarriedEvent` | a block comes loose in a quarry | `Quarrier` (the character), `Quarry` (its name), `Position` |

A server mod refuses one in `Stage.Validate` (`context.Cancel(HarvestRefusal.Other)`: a protected grove, a quest
tree) and reacts to one in `Stage.Observe` on any machine (experience, a contract, a log). Jobs' own `Validate` refuses
with `HarvestRefusal.Gone` when the tree or node is already down. A refusal reaches `Stage.Rejected` on the server and
on the player's client, and Jobs tells the player why. Jobs does its work in `Stage.Apply`: on the server it takes the
tree or node out and shares or gives the yield, on the clients near it plays the fall or the burst.

## Server owners

1. `.\MakeModFolder.ps1 Release`
2. Copy `Output/mods/Roleplay.Jobs/` into the server's `mods/` folder.
3. Tune `mods/Roleplay.Jobs/server/config.json`: which meshes are trees, reach, damage and health, the axes, their tiers, the
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

**Its own items.** Oblivion has no log or firewood item, so the mod makes them at run time through the SDK's
`ICustomItems`, with no plugin: each entry of `items` is a copy of a game-file item (`base`) with whatever of `name`,
`value`, `weight`, `damage`, `armor`, `mesh`, `scale`, `icon` (another item's inventory icon) and `enchantment` it sets
in place of the base's (the config's comment shows an enchantment's shape). A felled tree gives **Firewood** (a Stone
Brick shown with a static mesh and Gnarl Bark's icon, so it drops, bounces and sounds like what it looks like), and the **Lumber Axe** is an
Iron Battle Axe that does 1 damage in a fight and chops as a tier 1 axe. A drop, an axe or `giveOnJoin` names one as
`"custom:"` and its `key`; `giveOnJoin` gives each player one of each on joining when they carry none (the lumber axe,
by default). Custom items last one server session: a restart loses those in saved inventories.

## Not done yet

- **Carrying, seen in game:** picking up and putting down, the four assets, and the log's two fits, measured with bare
  hands drawn, and then the Beam fit. Not yet: the hold animation (looping, and on other players, whose arms come down
  only when it runs out), the switch between the fits, the Burden and the Speed drain. Box, Barrel and Sack hang at
  offset 0. It
  hangs on the body only, so in first person view it is where the body's hand is, not the arms'. Nothing yet gives a
  log from a felled tree, and a weapon in hand still shows next to what is carried.
- **Woodcutting by damage, normal attacks included, is untested in game**, as are lumber camps and the three
  networked events. Power attacks counting was seen working, once they connect. The
  felling, the fall and the stump were seen working with the old G key.
- The kinds' wood, girth, stump width and tiers, the axe tiers and the tier penalty are guesses: untested in game.
- `tools/TreeProbe` is a UE4SS Lua mod for single player (a plain launch: under the ReadyM launcher UE4SS loads mods
  from `%APPDATA%\ReadyM.Launcher\Oblivion\Mods` instead). Copy it to the game's `Binaries/Win64/Mods/`, then:
  Numpad 1 lists the instanced meshes around the player (class, mesh path, distance) to `UE4SS.log`; 2 removes the
  nearest tree the way the SDK does; 3 puts it back; 4 and 5 play the chop and mine montages in both views.
- `tools/HitBoxProbe` is a UE4SS Lua mod for finding out why a standing battle axe power attack often makes no impact
  on a tree. It works in multiplayer too, from `%APPDATA%\ReadyM.Launcher\Oblivion\Mods\HitBoxProbe`. Numpad 6 logs
  the player's weapon hit box to `UE4SS.log` whenever it turns on or off: where it is, how far from the player, and
  what it already overlaps. Numpad 7 does the same and also shows every hit box near the player while it can hit.
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
- **Custom items:** the firewood's mesh (`Wood01`) is a plank, waiting for a better pick. Its Gnarl Bark icon, armor
  ratings and enchantments are untested in game. Seen working in game: the lumber axe, firewood with its mesh, collision and sounds. A
  replaced mesh does not show on a weapon in hand yet.
- **Mining by damage, normal attacks included, is untested in game**, as are bark chips and chips flying back towards
  the one who struck. Before it, seen working: iron (KotN's vein, whose mesh comes from
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

The code is in the repository's `src/` and `tests/`, beside the gamemode's, and builds by its rules
(`Directory.Build.props`: warnings as errors, central package versions). This folder keeps the rest of the mod.

| Path | What |
|---|---|
| `src/Roleplay.Jobs.Common/` | the RPC contracts, the networked events (`JobEvents.cs`), and `TreeFilter`, which both halves use to agree on what a tree is |
| `src/Roleplay.Jobs.Server/` | `WoodcuttingService` and `MiningService` (the rules), `YieldShare` (who gets what), `JobAreas` (quarries and lumber camps), `FelledTreeStore` and `OreNodeStore` (the save files), `JobsConfig` |
| `src/Roleplay.Jobs.Client/` | `WoodcuttingClient` and `MiningClient`: the attacks; `TreeFallService`: the fall |
| `tests/Roleplay.Jobs.Tests/` | xUnit tests for the filters, the yields and shares, the areas, the save files and the shipped config |
| `mods/Jobs/Content/` | `config.json` (the server config shipped with the mod), `manifest.json`, `QuarryJob.esp`, the panel's page (`web/`) |
| `mods/Jobs/MakeModFolder.ps1` | packs the mod into `mods/Jobs/Output/mods/Roleplay.Jobs/` |
| `mods/Jobs/tools/` | the in-game probes (`TreeProbe`, `HitBoxProbe`) |

## Develop

```powershell
dotnet test Roleplay.Jobs.sln   # or the whole CyrodiilRP.slnx
.\MakeModFolder.ps1 Debug     # with .pdb files
```
