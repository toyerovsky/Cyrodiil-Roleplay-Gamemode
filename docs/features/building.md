# Building

## 1. What it is

Plots of land the staff mark out on a grid, and pieces of the game's own buildings, fences and furniture placed on them
from architect mode: a page over the game with a catalogue of pieces, each drawn by the game, dragged into the world
under the mouse, seen from a camera of the mode's own over the plot. For now only the staff on duty build, and
everything they place stands finished at once. A player buys a plot for sale at its sign.

## 2. Rules (as the staff see them)

- **The grid**: a tile is one square of the game's terrain, 128 of its units, 1.83 m. An exterior cell is 32 by 32
  tiles, so plots line up with the cells. A plot is a rectangle of whole tiles along the world's axes.
- **Marking a plot**: `/aplot new` opens architect mode. A drag on the ground from one corner to the other marks it,
  then a form asks its kind (Residential or Industrial), price and tax a week in gold, and its height in metres. Mark the
  plot saves it, stands its sign, and opens the plot to build on.
- **How a plot shows**: a green outline and its sides shaded green at 40% (`ILocalProps.PlaceFill`, a box filled in a
  translucent colour), the same on the plot built on, every plot while choosing, and the plot being marked. Only the
  sides across the plot from the view are shaded, as if each showed only its inner face, so the near ones never hide
  what is built behind them; from over the plot all four are. They follow the view as it moves.
- **Removing a plot**: Remove the plot in the panel while building on it, or Remove beside it in the list while
  choosing, asks first (Esc or Keep it leaves it). Removed, it goes with every piece on it as `/aplot remove` takes it,
  and whoever built on it goes back to the list.
- **The ground**: the land is seldom level, so a plot's ground is halfway between the heights where the drag began and
  ended. Its height counts from there. A plot reaches down without end: a piece may sink as deep as it likes, and the
  outline is drawn 50 m under the ground so it shows below a dip.
- **The front and the sign**: a plot's sign stands at the middle of its front, 60 cm outside it, its face turned away from
  the plot (the for-sale mesh reads along its own -X, so it stands turned back from the side's outward yaw), on the land
  there (`ILocalProps.TryFindGround`: a line down that goes on beneath trees, rocks and pieces to the
  landscape). While marking, the front is the side facing the camera, and the sign shows where it will stand. R or
  Turn the front moves the front to the next side, while marking or later on the plot built on (with nothing picked).
- A side is at most 64 tiles (`building.mostTiles`), gold from 0 to 1,000,000, a height from 1 to 100 m. A plot may not
  cover another plot of the area.
- **Choosing a plot**: `/abuild` off a plot opens architect mode on a list of the plots within 100 m, the nearest
  first, and their outlines in the world. A click on a plot's ground or its Build opens it to build on, New plot marks a
  new one (as `/aplot new`), and Plots in the bar goes back to the list from building or marking.
- **Building**: `/abuild` on a plot opens architect mode there, with the catalogue over the whole screen: the five
  categories as the game's tabs, each piece with its picture. A piece dragged out of it closes it and stands where the
  mouse lets go, on the ground or on the highest thing under its whole footprint, and is picked once it stands. C or
  Pieces opens the catalogue again. Its box shows green where it fits and red where not. A piece fits when every corner
  of its box stays on the plot's tiles and its top stays under the plot's height. It may sink into the ground. Pieces
  may overlap.
- A click on a piece picks it and leaves it where it stands: the game outlines it and a green box marks it. It moves only
  once the mouse goes more than 6 px with the button down. The mouse wheel turns the piece the mouse carries, otherwise
  it brings the view nearer.
- **The gizmo**: over a picked piece stand three arrows along the world's axes, red for X, green for Y, blue for up,
  sized to the view. A drag on one carries the piece along that axis only, shown as a carried piece is (green where it
  fits): along X and Y it snaps to the grid, up and down to the panel's height step from the plot's ground.
- **A picked piece** takes the keys: WASD or the arrows (keys or the buttons in its panel) move it along the ground, up
  being the axis of the grid nearest to where the camera looks, so it moves as the keys read on screen (10 cm steps with
  the grid off). Z held with W and S (or Shift+Up and Shift+Down) raises and lowers it 10 cm, Q and E turn it 15
  degrees (Shift: 1 cm, 1 degree). F (To the floor) lets it fall onto what is under it, or onto the plot's ground, as a
  carried piece settles; the game's own furniture cannot fall, it stands in its own way. Ctrl+C copies it, Ctrl+V puts
  the copy down under the mouse, with its turn, Ctrl+D (Duplicate) puts a copy where it stands, which fits wherever
  it does, to be moved off it with the keys. A copy is picked once it stands. Delete removes it, X lets go of it. With nothing picked, WASD
  move the view again.
- Every step goes to the next whole step in its direction, not by the step: at 34 cm, up by 25 is 50 then 75, down by
  25 is 25 then 0. Heights count from the plot's ground, turns from 0 degrees, the arrows from the grid's lines, so a
  piece off the grid comes back onto it with one press (`BuildingGrid.StepToward`). G changes the grid the mouse snaps to: whole,
  half or quarter tiles, or none.
- **Doors**: the Doors tab holds doors and gates the game animates where they stand (17: coastal, Chorrol, Imperial,
  Bruma, Skingrad and Leyawiin doors, a tower, cellar and ship's door, farm, stick, wooden and iron gates). Placed, each
  is the game's own door, made by the server as an SDK `Door` (its open or shut state is every player's), and it swings
  open when used. A door that leads elsewhere does not animate, so none such is offered: each one listed is one the
  game only ever places without a teleport (`tools/building/door_uses.py` counts both uses of every door in
  `Oblivion.esm`), and the TesterPlayground plugin's animated doors near Aleswell are among them.
- **Kit parts stand upright**: some kits are made lying down (Anvil's coastal cottages), and their houses stand each
  part up with a pitch or a roll. The client learns the turn each mesh has most often in the catalogue's houses
  (`ILocalProps.TryListParts`) and stands the part so when it is placed on its own, measured as it then stands.
- **Kits**: the Kits tab holds the parts of the game's own houses (walls, roofs, floors, beams, the insides), a kit
  each: Bruma's lower and middle houses and the middle one's inside, the town house and its insides, Anvil's three
  coastal cottages and their inside, Anvil's coastal town house and its inside. A chip over the parts picks the kit.
  The parts are made for one house each, not as blocks that fit any other: a kit's parts fit its own houses.
- **Break apart**: a picked house (or any piece of many meshes) is taken apart into the kit's parts it is built of,
  each where it stood, in one change: a wall can then be taken away, moved or duplicated, and the rest still fits. It
  is refused for the game's own furniture, a piece of one mesh, more than 200 parts (a farmhouse is hundreds of roof
  tiles), a part stretched or mirrored, and a part no kit lists (the client logs its mesh, for a kit to add).
- **The view**: architect mode looks down at the plot from a camera of its own, the plot whole at first, turned the way
  the player looked. WASD moves it over the ground, the right mouse button held turns it round and tilts it, the mouse
  wheel brings it nearer or further. It stays over the plot and up to 10 m past its edges (marking: within 120 m of
  where the player looked when it began). The character stands where it was.
- **Looking around**: B gives the game back its camera, the mouse and the keys to walk, the page folded to one line, and
  B brings architect mode and its view back.
- **The panel** at the left: the grid (whole, half or quarter tiles, or none), how far Q and E turn (90, 45, 15 or 5
  degrees, Shift 1), how far a piece goes up and down (1, 5, 10, 25, 50 or 100 cm, Shift 1), and what a piece stands on
  (what is under it, or the plot's ground).
- Esc lets go of what the mouse carries, then the catalogue, then the piece picked, then the plot marked, then leaves.
- Architect mode closes off duty, out of the world, more than 100 m from the plot, or when the plot is taken away.
- `/aplot remove` takes away the plot the staff member stands on with every piece on it. `/aplot list` lists them.
- The drawer's catalogue is `building.pieces` in `server/config.json`: a key (kept with every piece placed, so never
  renamed), a name, a category, the game's form, a scale and the plots it may stand on. A server that lists its own
  replaces the shipped one whole.

## 2a. Buying a plot (as a player sees it)

- **At the sign**: H before a plot's sign, within 3 m in front, opens the plot's page as it opens a notice board: its kind, size, how high it may be built, its price, its tax a week and the gold the player
  carries.
- **Buying**: Buy for the price takes the gold from the purse and makes the plot the character's. Shut, with the reason
  on the button, when the character carries too little, owns as many plots as a character may (`plotsPerCharacter` in
  `building` in `server/config.json`, 1 by default), or the plot is another's or theirs already.
- **The page closes** when the player walks more than 5 m off, the plot goes, or the player leaves the world. It follows
  the purse and the plot while open: a plot bought by someone else meanwhile shows as taken.
- Building on one's own plot is not here yet: see Not built yet.

## 3. Ways in

| Way in | What it does | Code |
|---|---|---|
| `/aplot new`, `remove`, `list` | Staff on duty: mark a plot, take the one stood on away, list them | `Building/BuildingCommands.cs` |
| `/abuild` | Staff on duty: architect mode on the plot stood on, or the plots near to choose from | `Building/BuildingCommands.cs` |
| The architect page | Catalogue (C), picking, the plot's form, Leave | `ui/architect`, `Building/ArchitectOverlay.cs` (client) |
| B | Look around and back, only in architect mode | `Building/ArchitectOverlay.cs` (client) |
| H before a sign | Any player: the plot page, Buy | `Building/PlotSales.cs`, `ui/plot`, `Building/PlotSaleOverlay.cs` (client) |
| WASD, right mouse button, wheel | Move, turn and bring the view nearer, in architect mode | `Building/ArchitectCamera.cs` (client) |

Both commands are an administrator's. Reading a sign is everyone's.

## 4. How it works

- **Database**: `plots` (area, first tile, width and depth in tiles, ground height, height, kind, price, weekly tax,
  owner, the sign's spot) and `plot_objects` (plot, the piece's key, position, rotation, state). A plot taken away takes
  its pieces with it. A deleted owner leaves the plot for sale.
- **In memory**: `Plots` keeps the plots (`PlotRegistry`), the pieces on them (`PlotObjectRegistry`) and the catalogue
  (`PieceRegistry`). Every change is written first and kept after.
- **The pieces in the world**: each player's game stands the signs and pieces of the area its character is in as meshes
  of its own, with collision (`Building/BuildingProps.cs`, server and client), as the notice boards do.
- **The view**: `Building/ArchitectCamera.cs` puts the SDK's `ICamera` at a distance from a point on the ground, looking
  down at it (`ICamera.Set`, a glide in and out, then set again each frame it moves), and gives the game's camera back
  on leaving or B (`ICamera.Reset`). The page sends WASD as held, the right button's pixels and the wheel's notches.
  The page holds the input meanwhile, so the character does not walk. A load takes the camera away: it is set again.
- **The mouse in the world**: the page sends where the mouse is as a fraction of the screen. The client asks the SDK's
  `ILocalProps.TryTraceScreen` for the line the camera casts through that point and what it hits first: the ground, or a
  piece (`Building/ArchitectWorld.cs`). The piece carried is a mesh without collision, which the line passes through, and
  a piece picked to move is taken down while it is carried. The piece's box is let fall from a little above that point
  (`ILocalProps.FindRest`) at the snapped spot, so it rests on what is under its whole footprint.
- **The game's own objects**: a catalogue piece can be `Furniture` or a `Container` with its form (`object` and `form`
  in `building.pieces`): beds, chairs, stools and benches to sit on or sleep in, and empty chests, cupboards and drawers
  to open. The server makes it in the cell it stands in under a ref id of its own (`Building/BuildingReferences.cs`),
  `plot_objects.ref_id` keeps it, and the cell's save keeps the object. The players' games show it as the game's own,
  and architect mode picks it by its ref id (`ILocalProps.TryTraceScreen` reports `ScreenHit.Reference`). Its yaw is
  given less 90 degrees: an object's actor faces its heading itself, unlike a character's pawn
  (`BuildingReferences.ObjectYawOffset`). Pieces placed
  before a piece became furniture stay meshes.
- **Whole pieces**: a form the Remaster builds of a kit (a house, a stable, a well) is a blueprint of many meshes. The
  SDK's props stand every mesh of the blueprint's construction script, each where the blueprint places it. An instanced
  mesh (a roof of tiles) stands once for each of its instances, a mirrored one (the other half of a roof) mirrored too.
- **The outline**: `ILocalProps.Highlight` gives the prop's meshes the custom depth stencil the player's character
  outlines what it looks at with (`HighlightCustomStencil`), so the game's own outline draws it. The game's furniture
  is not a prop: only the green box marks it.
- **Kits in the catalogue**: `building.kits` in `server/config.json` (the shipped ones in `ShippedKits.cs`, written by
  `tools/building/make_kits.py` from the game's own list of assets, which `tools/building/list_game_assets.py` reads out
  of the game's `.utoc`). A kit names its meshes by their folder under the game's architecture, and each mesh is a piece
  keyed `kit.<its name>`, so a key never changes while the game keeps the mesh.
- **Taking apart**: the SDK's `ILocalProps.TryListParts` gives every mesh a form stands as, with its place, turn and
  scale from the form's origin (the same parts the props stand). The client turns each by the piece's yaw from its
  origin and sends them (`ArchitectBreakApart`), the server checks every part is a kit's mesh that fits the plot, and
  `PlotBook.ReplaceObjectAsync` takes the piece away and writes the parts in one transaction. A part keeps its pitch
  and roll.
- **Pictures**: the client draws each piece once a session, one at a time while architect mode is open
  (`ILocalProps.DrawPicture`: the piece stands in full detail, out of sight, for ten frames, then a scene capture
  draws its own colours, unlit, `Building/PiecePictures.cs`), the pieces the drawer shows first, and hands the page a
  PNG with a clear background. A piece not drawn yet shows an empty frame.
- **Fit**: `PlotBox.Judge` (in `CyrodiilRP.Common`, so the client and the server judge alike) turns the piece's box by
  its yaw and holds every corner to the plot. The server cannot read a mesh: the first architect's game to carry a piece
  measures it (`ILocalProps.TryMeasure`) and sends the box, which the server keeps until a restart. A piece nobody
  measured yet is judged by its origin.
- **Checks**: the server checks every change again (`Architects`, `ArchitectRules`): the spot, the piece, the fit, the
  plot's form and that the sign stands at the plot's edge.
- **Buying**: `PlotSales` judges the buyer (`PlotSaleRules.Judge`), takes the gold, then writes the owner with
  `PlotBook.BuyAsync`, a single update that holds only while the plot has no owner, so of two buyers at once one gets it
  and the other has the gold back. A failed write gives the gold back too. Audit `economy.plot_buy`.
- **Doors taken away**: removing a door piece deletes the server's `Door` with it. A door made before deletion knew doors
  stayed in its cell's save: at load, every door the server made of a catalogue door that no piece holds is deleted
  (`BuildingReferences.SweepDoors`, by the SDK's `ServerRefIds.IsServerMade`).
- **Audit**: `plot.mark`, `plot.sign`, `plot.remove`, `building.place`, `building.move`, `building.break_apart` and
  `building.remove`.

## 5. Not built yet

- Players: building on their own plot, moving only their own pieces, selling a plot back.
- Materials for each piece, the plot's storage in a window, blueprints built when the storage holds the materials.
- The weekly tax and what happens when it is not paid.
- Removing the flora on a plot (trees, bushes and rocks are the SDK's `IFoliage`, plants are `Flora` entities).
- Snapping to the piece next to the one carried.
- The view ignores what stands in its way: tilted low behind a hill or a house it looks through them.
- Interiors, doors and entering a building.
- A carried piece of furniture leaves the object where it stood until the server moves it.
- The pictures still show the far detail of some meshes.
- Snapping a kit's part to the next one: a part from the drawer snaps to the grid, not to the parts beside it.
- The page gets the whole catalogue (about 560 pieces) with every change, about 70 KB.
- A door placed once stood at another angle than the one shown while carried. It did not show again after architect
  mode was opened anew, and its cause is not known.
- Not checked in the game yet: the plot page at a sign and buying, removing a door and the sweep of doors left behind,
  the doors (that they swing for every player, and their turn), the shaded sides and the gizmo (the fill's colour and opacity are set under the names
  the engine's own coloured materials use, `Color` and `GizmoColor` with its alpha, and `Opacity`, unchecked on the
  game's translucent debug material), the kits' parts and their pictures, breaking a house apart, the view (`ICamera.Set` has not run in the game anywhere yet), that the mouse finds the
  world through it, the glide back on leaving, the pieces as other players see them, the game's own furniture and
  containers (sitting, sleeping, opening, moving, removing, after a restart), the unlit pictures.
