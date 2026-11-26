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
as DirectX has them, roughness, metallic) and exports it as FBX, and `unreal/build_pak.ps1` imports them into an Unreal
Engine 5.3.2 project named as the game (the remaster's engine) and cooks them into `CyrodiilRP_P.pak`, `.utoc` and
`.ucas` for the game's `Content\Paks\~mods`:

```powershell
$blender = "C:\Program Files\Blender Foundation\Blender 4.2\blender.exe"
& $blender -b --factory-startup -P design/models/fishing-rods/make_rods.py -- <dir> --no-render
& $blender -b --factory-startup -P design/models/fishing-rods/export_rods.py -- <dir>/fishing_rods.blend <export dir>
design/models/fishing-rods/unreal/build_pak.ps1 -Source <export dir> -Work <unreal dir> -Out <dir> -Engine "C:\Program Files\Epic Games\UE_5.3"
```

A rod lies along +Y in Blender, the hand at the origin, the tip where a staff's head is, so it hangs on the hand as the
game's staves do. The container holds no material: the game's engine is Unreal 5.3.2 changed by its makers, and a
material the stock engine cooks crashes it on load ("Bad export index"). The rods' material instances take the game's
own `MI_T_BaseMaterial_FPSClippingFix`, which the project stands in for at its path and never ships. The cook saves
each property with its name (`-VersionCookedContent`): the game's classes carry fields stock 5.3.2 lacks (three more
in `FBodyInstance`, from 5.4), so properties saved by their place in the class, as a cook does by default, land on
the wrong fields and crash it. The game's own mappings (`Mappings.usmap` of the research) show its classes' fields. The client holds `/Game/CyrodiilRP/Fishing/SM_FishingRod_<Grade>` when the game has it, and the
grade's staff when not.