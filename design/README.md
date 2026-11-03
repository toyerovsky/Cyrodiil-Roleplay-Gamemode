# Design

Pictures for the pages that the game does not have. The pages use them from `ui/rp/art` as webp.

## Recraft

The Recraft MCP server (`mcp__recraft__*` in Claude Code) pays with the credits of the Recraft account. Each picture
keeps its original and its cut-out in `recraft/<group>/`, and `recraft/to_ui.py` makes the page's copy:

```powershell
python -I design/recraft/to_ui.py design/recraft/<group>/<name>_cut.webp ui/rp/art/<name>.webp <size>
```

| Picture | Page copy | How |
|---|---|---|
| `recraft/seal/imperial_seal.webp`, `imperial_seal_cut.webp` | `ui/rp/art/seal.webp`, 200 px, shown at 104 | `recraftv4_1_raster`, 1:1, one image, then `remove_background` (3 credits). The prompt: "A single round red sealing wax seal seen straight from above, deep crimson wax with irregular poured and squashed edges, glossy highlights, the centre stamped with an embossed coiled dragon emblem inside a ring of small laurel leaves, in the style of the Septim Empire of Cyrodiil from The Elder Scrolls, medieval, realistic material, soft studio light, centred, isolated on a plain flat light grey background, no text, no letters, no ribbon, no paper" |

The seal closes the name letter (`ui/creator`): faded while the name is not whole, pressed once it is.
