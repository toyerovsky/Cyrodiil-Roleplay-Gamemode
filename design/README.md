# Design

Pictures for the pages and models that the game does not have. The pages use the pictures from `ui/rp/art` as webp.

## Recraft

The Recraft MCP server (`mcp__recraft__*` in Claude Code) pays with the credits of the Recraft account. Each picture
keeps its original and its cut-out in `recraft/<group>/`, and `recraft/to_ui.py` makes the page's copy (quality 88
unless given, for the colour and the edges both). A picture shown in a circle that fades out keeps no cut-out, and
`recraft/to_round.py` makes its copy instead:

```powershell
python -I design/recraft/to_ui.py design/recraft/<group>/<name>_cut.webp ui/rp/art/<name>.webp <size> [quality]
python -I design/recraft/to_round.py design/recraft/<group>/<name>.webp ui/rp/art/<group>/<name>.webp <size> [quality]
```

| Picture | Page copy | How |
|---|---|---|
| `recraft/seal/imperial_seal.webp`, `imperial_seal_cut.webp` | `ui/rp/art/seal.webp`, 200 px, shown at 104 | `recraftv4_1_raster`, 1:1, one image, then `remove_background` (3 credits). The prompt: "A single round red sealing wax seal seen straight from above, deep crimson wax with irregular poured and squashed edges, glossy highlights, the centre stamped with an embossed coiled dragon emblem inside a ring of small laurel leaves, in the style of the Septim Empire of Cyrodiil from The Elder Scrolls, medieval, realistic material, soft studio light, centred, isolated on a plain flat light grey background, no text, no letters, no ribbon, no paper" |
| `recraft/specializations/<name>.webp`, `<name>_cut.webp`, one per specialization | `ui/rp/art/specializations/<name>.webp`, 128 px (83 KB for the nine), shown at 64 | The private style "Cyrodiil RP parchment ink" (`f5451ab9-ab6b-4f33-a85f-b5e138cf2084`), made from `lore-book.webp`, `d20.webp` and `gambling.webp` so the pictures match them; 1:1, two images each, the better one, then `remove_background` (about 60 credits for the nine and the retries). Each prompt is "A <name>'s emblem standing free with no frame: <subject>, hand-drawn medieval ink illustration, bold dark brown outline, engraving hatching, muted sepia and parchment colours with <accent>, centred, isolated on a plain flat white background, no plaque, no badge, no backing shape, no border, no text". Subjects: knight, a heater shield with a rearing dragon in faded red, a longsword and a flanged mace crossed behind it; barbarian, a battle axe with a fur pelt and a bone fang charm; ranger, a longbow and a quiver of fletched arrows with oak leaves; wizard, a long gnarled staff held diagonally, a claw gripping an orb in flame and blue sparks; alchemist, a mortar and pestle, a round flask of bubbling potion and herbs; illusionist, an ornate half mask under curling smoke; healer, a rolled linen bandage with a red ribbon, a vial of red draught and white flowers; smith, an anvil with a hammer and tongs crossed above it and sparks; thief, an iron ring of lockpicks and a skeleton key, a dagger and a coin pouch. Asking for "no cross" keeps a Christian cross off the shields and pouches |
| `recraft/guides/<topic>.webp`, one per guide | `ui/rp/art/guides/<topic>.webp`, 512 px (about 430 KB for the eleven), shown at 520 in a circle fading out | `recraftv4_1_raster`, 1:1, two images each, the better one, no cut-out (about 46 credits for the eleven), then `recraft/to_round.py` makes the page's copy: the corners the circle hides one flat colour. Each prompt is "Sepia ink and graphite sketch on aged parchment, in the manner of a Renaissance artist's notebook study, fine cross-hatching, warm umber and brown ink with soft graphite shading, high fantasy medieval empire. Subject: <subject>. Centered composition, the drawing fades softly into bare parchment toward the edges. No text, no letters, no words, no frame, no border." Subjects: welcome, a hooded traveller seen from behind on a stone road, looking at a walled city on a lake island with one tall slender white tower; trader, a shopkeeper weighing goods on brass scales before shelves of bottles, bread and cheese; bank, a banker behind a carved counter with stacks of coins, a ledger and a strongbox; stable, a stablemaster in a leather apron holding a saddled horse by a timber stable; mentor, a grey-bearded master at a lectern with a sword and shield, a staff and flasks, and a hammer on an anvil; foreman, a foreman with a pickaxe at a quarry, a cart of cut stone, firewood and a dock crane; notice board, a roofed board on a post in a cobbled square, notices pinned with nails; gambling, two players' hands at a tavern table with dice, a dice cup, a pot of coins and tankards; dice, polyhedral dice tumbling across a tray by a dice bag; lore, an open journal with a sketched portrait and handwriting, a quill and a seal; exchange, two travellers striking a deal, a coin purse for a wrapped sword |

The seal closes the name letter (`ui/creator`): faded while the name is not whole, pressed once it is. The
specializations' pictures stand beside each specialization in the mentor's window (`ui/mentor/pictures.js`). The
guides' pictures stand on the right of each guide's card (`ui/guides/pictures.js`) and, small, in the menu's Help tab.

## Models

The mode's own meshes. `models/fishing-rods/make_rods.py` builds the three fishing rods in Blender 4.2 from code
(`fishing_rods.blend`, with previews unless given `--no-render`), `export_rods.py` bakes each on a UV map of its own
into the two textures the game's base material takes (colour with the occlusion in it, and NNRM: the normal's X and Y
as DirectX has them, roughness, metallic) and exports it as FBX, with five bent meshes of it for a fish's pull,
`check_export.py` reads the export back, measures it and renders previews, and `unreal/build_pak.ps1` imports the
meshes into an Unreal Engine 5.3.2 project named as the game (the remaster's engine) and cooks them into
`CyrodiilRP_P.pak`, `.utoc` and `.ucas`. They are kept in `mod/paks` (Git LFS, as the server cannot run Unreal),
`pack.ps1` puts them into the mod's client half, and the launcher copies every `.pak`, `.utoc` and `.ucas` of a
server's mods into the game's `Content\Paks\~OblivionMp` as a player joins, before the game starts and mounts them:

```powershell
$blender = "C:\Program Files\Blender Foundation\Blender 4.2\blender.exe"
& $blender -b --factory-startup -P design/models/fishing-rods/make_rods.py -- <dir> --no-render
& $blender -b --factory-startup -P design/models/fishing-rods/export_rods.py -- <dir>/fishing_rods.blend <export dir>
& $blender -b --factory-startup -P design/models/fishing-rods/check_export.py -- <export dir> <preview dir>
design/models/fishing-rods/unreal/build_pak.ps1 -Source <export dir> -Work <unreal dir> -Out <pak dir> -Engine "C:\Program Files\Epic Games\UE_5.3"
```

The container is built outside the repo and copied into `mod/paks` only once the offline check of the game-assets
skill ("Checking a container without the game") has passed for every asset in it: from `mod/paks` it reaches every
player who joins.

A rod lies along +Y in Blender, the hand at the origin, the tip where a staff's head is, so it hangs on the hand as the
game's staves do. Its bent meshes, `SM_FishingRod_<Grade>_Bend1` to `_Bend5` (`BENDS` in `export_rods.py`:
10, 22, 36, 52 and 70 degrees at the tip), are the baked rod with its blank turned into an arc of the same length toward the
guides from the top of the grip on, so they keep its UVs, textures and material instance, and the grip, the butt and
the reel stay where the hand holds them. `rods.json` in the export lists every mesh with its box and the centre of its
tip ring, where the line leaves the rod, in Unreal's centimetres: `check_export.py` and the import both hold the
meshes to it, and the client's constants come from it.

The container holds no material: the game's engine is Unreal 5.3.2 changed by its makers, and a
material the stock engine cooks crashes it on load ("Bad export index"). The rods' material instances take the game's
own `MI_T_BaseMaterial_FPSClippingFix`, which the project stands in for at its path and never ships. The cook saves
each property with its name (`-VersionCookedContent`): the game's classes carry fields stock 5.3.2 lacks (three more
in `FBodyInstance`, from 5.4), so properties saved by their place in the class, as a cook does by default, land on
the wrong fields and crash it. The game's own mappings (`Mappings.usmap` of the research) show its classes' fields. The client holds `/Game/CyrodiilRP/Fishing/SM_FishingRod_<Grade>` when the game has it, and the
grade's staff when not.

## Animations

The mode's own animations. **Not yet loaded in the game:** the first one, the angler's reeling, passes every check
that can be run without the game and nothing more. Its container stays out of `mod/paks` until it has been tried by
hand in the game's `Content\Paks\~mods`.

`models/fishing-anim/make_reel.py` makes `/Game/CyrodiilRP/Fishing/A_Fishing_Reel` in Blender 4.2 from code: the game's
`A_Humanoid_Staff_Block_Idle` (the pose the client holds through a fight, 91 keys over 3 s) with the left arm taken
off the rod's butt and put on the reel's handle, where the hand goes round the axle six times, the arm following by
two-bone IK. It reads the game's skeleton and the base's keys as the research repo's `UeExport` wrote them, and writes
every key of every track as JSON, with the game's skeleton as FBX for the stand-in skeleton. `unreal/build_pak.ps1`
makes an Unreal Engine 5.3.2 project named as the game, imports the stand-in at the game skeleton's path
(`import_skeleton.py`), writes the keys into a new sequence through the animation data controller (`import_reel.py`)
and cooks it into `CyrodiilRPAnim_P.pak`, `.utoc` and `.ucas`, a container of its own beside the rods' (one project can
cook both once the animation has been seen in the game). `check_reel.py` holds the cooked sequence to the game's base
sequence and to the keys written, `preview_reel.py` draws it on the game's figure with a rod in its hand:

```powershell
# In the research repo: the game's skeleton and the base's keys. base.txt holds the base's object path,
# /Game/Art/Animation/Humanoid/ThirdPerson/Staff/Combat/Block/A_Humanoid_Staff_Block_Idle.A_Humanoid_Staff_Block_Idle
dotnet run -c Release --project tools/ue/UeExport -- json GAME_DIR <game json> --usmap tools/bin/usmap/Mappings.usmap OblivionRemastered/Content/Art/Character/Humanoid/SKEL_HumanoidSkeleton
dotnet run -c Release --project tools/ue/UeExport -- anims GAME_DIR <game keys> --usmap tools/bin/usmap/Mappings.usmap base.txt

$blender = "C:\Program Files\Blender Foundation\Blender 4.2\blender.exe"
& $blender -b --factory-startup -P design/models/fishing-anim/make_reel.py -- <game json>/OblivionRemastered/Content/Art/Character/Humanoid/SKEL_HumanoidSkeleton.json <game keys> <dir>
design/models/fishing-anim/unreal/build_pak.ps1 -Source <dir> -Work <unreal dir> -Out <pak dir> -Engine "C:\Program Files\Epic Games\UE_5.3"

# The offline check: <folder>/OblivionRemastered/Content/Paks holds hard links to the game's containers and the three
# files of <pak dir>. both.txt holds the mode's sequence (/Game/CyrodiilRP/Fishing/A_Fishing_Reel.A_Fishing_Reel) and the base.
dotnet run -c Release --project tools/ue/UeExport -- json <folder> <json> --usmap tools/bin/usmap/Mappings.usmap OblivionRemastered/Content/CyrodiilRP/ Engine/Content/Animation/ OblivionRemastered/Content/Art/Character/Humanoid/SKEL_HumanoidSkeleton OblivionRemastered/Content/Art/Animation/Humanoid/ThirdPerson/Staff/Combat/Block/A_Humanoid_Staff_Block_Idle
dotnet run -c Release --project tools/ue/UeExport -- anims <folder> <decoded> --usmap tools/bin/usmap/Mappings.usmap both.txt
python -I design/models/fishing-anim/check_reel.py <dir> <json> <decoded>
& $blender -b --factory-startup -P design/models/fishing-anim/preview_reel.py -- <research repo>/tools/catalog <decoded> /Game/CyrodiilRP/Fishing/A_Fishing_Reel.A_Fishing_Reel <rods export dir>/SM_FishingRod_Master.fbx <preview dir>
```

A cooked sequence is tied to its skeleton three ways, and the stand-in shares all three with the game's: the bones'
order (a track names its bone by index: the five FBX files give Unreal the game's order, and the import refuses any
other), the guid (written into the stand-in's file between the two runs of the editor) and the virtual bones (the
cook bakes a track for each). The sequence names the engine's own compression settings assets, as the game's
sequences do, not the ACL plugin's, which a new sequence of a stock project takes. The game-assets skill has the
rules and what taught them.

The client plays a pose again 0.2 s before it ends (`AnglerLooks.PoseOverlap`), and the handle turns every 0.5 s: played
so, the hand jumps 0.4 of a turn at each replay. An overlap of a whole turn (0.5 s) for this pose keeps the hand on
its circle.
