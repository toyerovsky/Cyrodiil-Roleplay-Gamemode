# Design

Pictures for the pages that the game does not have. The pages use them from `ui/rp/art` as webp.

## Recraft

The Recraft MCP server (`mcp__recraft__*` in Claude Code) pays with the credits of the Recraft account. Each picture
keeps its original and its cut-out in `recraft/<group>/`, and `recraft/to_ui.py` makes the page's copy. A picture shown
in a circle that fades out keeps no cut-out, and `recraft/to_round.py` makes its copy instead:

```powershell
python -I design/recraft/to_ui.py design/recraft/<group>/<name>_cut.webp ui/rp/art/<name>.webp <size>
python -I design/recraft/to_round.py design/recraft/<group>/<name>.webp ui/rp/art/<group>/<name>.webp <size> [quality]
```

| Picture | Page copy | How |
|---|---|---|
| `recraft/seal/imperial_seal.webp`, `imperial_seal_cut.webp` | `ui/rp/art/seal.webp`, 200 px, shown at 104 | `recraftv4_1_raster`, 1:1, one image, then `remove_background` (3 credits). The prompt: "A single round red sealing wax seal seen straight from above, deep crimson wax with irregular poured and squashed edges, glossy highlights, the centre stamped with an embossed coiled dragon emblem inside a ring of small laurel leaves, in the style of the Septim Empire of Cyrodiil from The Elder Scrolls, medieval, realistic material, soft studio light, centred, isolated on a plain flat light grey background, no text, no letters, no ribbon, no paper" |
| `recraft/guides/<topic>.webp`, one per guide | `ui/rp/art/guides/<topic>.webp`, 512 px (about 430 KB for the eleven), shown at 520 in a circle fading out | `recraftv4_1_raster`, 1:1, two images each, the better one, no cut-out (about 46 credits for the eleven), then `recraft/to_round.py` makes the page's copy: the corners the circle hides one flat colour. Each prompt is "Sepia ink and graphite sketch on aged parchment, in the manner of a Renaissance artist's notebook study, fine cross-hatching, warm umber and brown ink with soft graphite shading, high fantasy medieval empire. Subject: <subject>. Centered composition, the drawing fades softly into bare parchment toward the edges. No text, no letters, no words, no frame, no border." Subjects: welcome, a hooded traveller seen from behind on a stone road, looking at a walled city on a lake island with one tall slender white tower; trader, a shopkeeper weighing goods on brass scales before shelves of bottles, bread and cheese; bank, a banker behind a carved counter with stacks of coins, a ledger and a strongbox; stable, a stablemaster in a leather apron holding a saddled horse by a timber stable; mentor, a grey-bearded master at a lectern with a sword and shield, a staff and flasks, and a hammer on an anvil; foreman, a foreman with a pickaxe at a quarry, a cart of cut stone, firewood and a dock crane; notice board, a roofed board on a post in a cobbled square, notices pinned with nails; gambling, two players' hands at a tavern table with dice, a dice cup, a pot of coins and tankards; dice, polyhedral dice tumbling across a tray by a dice bag; lore, an open journal with a sketched portrait and handwriting, a quill and a seal; exchange, two travellers striking a deal, a coin purse for a wrapped sword |

The seal closes the name letter (`ui/creator`): faded while the name is not whole, pressed once it is. The
guides' pictures stand on the right of each guide's card (`ui/guides/pictures.js`) and, small, in the menu's Help tab.
