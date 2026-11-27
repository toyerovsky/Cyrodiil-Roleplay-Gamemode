# Fisher

## 1. What it is

A fisher NPC buys the catch, sells fishing rods, baits and the spoon lure, mends worn rods, and shows the records: the
longest fish of every species landed on the server. Fishing itself is in `docs/features/fishing.md`.

## 2. Rules (as the player sees them)

- The "Services" topic in the game's dialogue with a fisher opens the fisher's window: the gold carried, the fish and
  trophies carried with what one fetches, the shop, and the worn rods carried with what mending one costs.
- **Selling the catch**: a fish or trophy fetches its item's value (a trophy three times its fish's). A deal sells from 1
  to 20 of one kind, and "Sell all" sells every fish and trophy carried at once. The fisher buys nothing else.
- **The shop**: the three grades of rod, good, and the mode's own baits, at their catalogue prices:

  | Ware | Price | Weight |
  |---|---|---|
  | Fishing Rod | 25 | 3 |
  | Sturdy Fishing Rod | 120 | 3.5 |
  | Master Fishing Rod | 450 | 4 |
  | Fishing Worms | 1 | 0.1 |
  | Dough Bait | 2 | 0.1 |
  | Spoon Lure | 15 | 0.1 |

  A rod shows how far it casts and how much strain its line takes, a bait how many the player carries. A deal buys from
  1 to 20 pieces, paid from the gold carried. The game's crab meat is a bait too, but the fisher does not sell it. The
  general trader still sells the plain rod.
- **Mending**: a worn, damaged or broken rod carried comes back good, one at a time, for 15% of the rod's price for each
  step of wear, rounded up: a Fishing Rod 4, 8 and 12 gold, a Master Fishing Rod 68, 135 and 203.
- **Records**: the fourth tab lists the thirteen species in the catalogue's order, each with the length of the longest
  one landed. The species picked shows its five longest fish, one a character (a character's best of the species):
  the place, the angler, the length in cm, the weight (grams under a kilogram, kilograms to one decimal from there) and
  the day it was landed. A species nobody has landed says so in one line.
  - The place is the number of characters with a longer best, plus one. Fish of one length share a place and stand in
    the order they were landed, so five fish of 80 cm are all first and the reader's own of 80 cm, landed later, is
    first too, under them.
  - The reader's own fish is marked "you". When it is not among the five it stands under them as "Your best", with its
    place among every character's.
  - An angler is named as the lore card allows. One in the game reads as everywhere else: the name to those introduced
    and when the owner made it public, the race to a stranger. One not in the game is named only when the name is
    public, and is "Stranger" otherwise, also to those introduced: the game tells a race only of a character in the
    world, and who knows whom is held only for the characters in play.
  - The board is what the database held at most a minute ago, for everyone. The reader's own fish stand as they are
    now, so a catch is on its angler's board at once and on everyone else's within a minute or so.
- Every deal the server refuses says why in the window, in the player's language: too poor, not carried, more than 20
  at a time, a purse that cannot hold the gold. A board that cannot be read is refused the same way.
- The window shows what changes while it is open (gold, the catch, rods and baits carried) within a second.
- The window closes when the player walks more than 5 m from the fisher, leaves the world, or the fisher is taken away.
- A fisher is labelled "Fisher" ("Rybak" in Polish) over the head and on the crosshair and, as a trader, takes no harm.
- **Raw fish are food**: eaten from the game's inventory, a fish or trophy fills the hunger meter of the Hunger and
  Thirst mod: 40 satiety and 70 more for each unit of the item's weight (a perch 75, a pike 180, the golden carp 215), a
  trophy half as much again, and 10 hydration. The satchel (Alt+H) and Feed on the H wheel do not offer fish.

## 3. Ways in

| Way in | What it does | Code |
|---|---|---|
| `/anpc here fisher` | Staff: stands a fisher where the staff member stands | `Traders/TraderCommands.cs` |
| `/anpc remove`, `list` | Staff: takes the fisher in front away, lists fishers among the traders | `Traders/TraderCommands.cs` |
| The game's dialogue with a fisher, topic "Services" | Opens the fisher's window | `Fisher/FisherShops.cs`, `docs/features/npc-dialogue.md` |
| The window | Buy, Sell, Sell all, Mend, Esc or Leave | `ui/fisher`, `Fisher/FisherOverlay.cs` (client) |
| The window's Records tab (a click, Q and E) | Asks for the records, the arrows pick a species | `Fisher/FisherRecords.cs`, `ui/fisher/RecordsTable.vue` |
| The game's inventory | Eating a fish | `Sustenance/FishFoodRegistration.cs`, Hunger and Thirst's `SustenanceService` |

There is no player command. The staff's panel lists a fisher on its World tab as a trader of the shop `fisher`.

## 4. How it works

- **The NPC** is a trader spot whose shop is `fisher` (`TraderRules.FisherShop`, `TraderSpotKind.Fisher`): the same row
  in `trader_spots`, stood, kept, adopted and kept from harm by `TraderNpcs`, under an origin of its own
  (`TraderOrigin.FisherSpawnerRefId`) by which every client labels it (`TraderMarkers`, `TraderCrosshair`, the label
  sent with `FisherLabel`), in the game's Fishing Waders (`LowerShirt09`) and rough leather shoes. No shop of
  `stocks.json` may be named `fisher`: loading throws.
- **The wares** (`FisherWares`) come from the fishing catalogue (`Fishing/RodCatalog`, `Fishing/BaitCatalog`): every
  rod kind in its good condition and every bait with a price, by its custom item's key. The catch is every
  `FishCatalog` item, bought at its value. A rod is mended with `FishingStock.TryExchange` from its worn item to the good
  one, at `RodKind.RepairPrice`.
- **Checks** (`FisherChecks`): the count from 1 to `FisherRules.MostPieces` (20), the gold for a purchase or a repair,
  the pieces carried for a sale. A key the fisher does not deal in is refused and logged as a warning.
- **Gold and items** (`FisherShops`): a purchase takes the gold, then gives the items, and gives the gold back if they
  cannot be given. A sale takes the fish, then gives the gold, and gives the fish back if the purse cannot hold it (a
  catch worth more than a purse holds is refused before anything moves). A repair takes the gold, then exchanges the
  rod, and gives the gold back if it cannot.
- **The window** gets `FisherState` (a `FisherView`: `gold`, `most`, `catch`, `shop` and `repairs`, JSON in camelCase
  with enums by name) as it opens, after each deal and whenever it would read otherwise, checked every second
  (`fisher.watch`), and `FisherAnswer` after each deal. The records are not part of it: they come when asked for. The
  icons are the painted ones of `ui/shared/fishing`: a rod's by its grade (`rod`, `rod-sturdy`, `rod-master`), a bait's
  its own (`worms`, `dough`, `spinner`).
- **The records** (`FisherRecords`): the page sends `FisherAskRecords` each time the Records tab opens and gets one
  `FisherRecords` back, a `FisherRecordsView` (`species`: each a `species`, a `name`, a `top` of up to
  `RecordBoardRules.TopCount` (5) fish and an `own`, null unless the reader's best lies under them; a fish is `rank`,
  `name`, `lengthCm`, `grams`, `at` in Unix milliseconds and `mine`). Only a player with the fisher's window open is
  answered.
  - **One reading a minute** (`RecordBoardKeeper`): the last reading of the database is kept with when it began, and
    everyone who asks within `RecordBoardRules.KeptFor` (1 min) gets that same reading, finished, still on its way or
    failed. A failed one is logged as an error and answered with `FisherAnswer` (`fisher.records_unread`).
  - **The reading** (`FishingBook.ReadBoardAsync`, on `Persistence.Reader`): two queries in one repeatable-read
    transaction over `character_fish_records`, the five longest of each species (a `ROW_NUMBER` over the species, by
    length, then the day, then the character) joined with `characters` for the name and `name_public`, and the count of
    characters at every length of every species, by which any length is ranked. It has no index of its own: with every
    one of 2 000 characters holding all thirteen species (26 000 rows) the reading took 36 ms on the test PostgreSQL,
    with 20 000 characters (260 000 rows) 340 ms, and an index on the species and the length only halved that, since
    the window function reads every row either way. Past that size the five of each species want a query of their own
    over such an index.
  - **For each reader** (`RecordBoardViews`, on the update thread): the reader's own records come from `FishingSkills`
    as they are now and take the place of what the reading holds of them, the fish are ordered and ranked again, and a
    best that falls outside the five is ranked by the counts. `RecordNames` names an angler: `ShownNames.Shown` for one
    in the game, the name only if `name_public` for one who is not, `GameRaces.StrangerLabel` otherwise.
- **Audit**: `economy.fisher_buy` (the ware, the count, the gold paid), `economy.fisher_sell` (the items and counts, the
  pieces, the gold got) and `economy.fisher_repair` (the rod, the gold paid).
- **Food**: `FishFoods` turns every catalogue fish into a `CustomFood` and `FishFoodRegistration` registers them with
  `ISustenance.RegisterCustomFood` at start. A meal from the game's inventory names a custom item by its network id,
  whose form id is 0: Hunger and Thirst finds its key through `ICustomItems.TryFind` and feeds by the registered values.
  The satchel and Feed go through `MealEvent`, which names its item by a form id, so they leave custom foods out.

## 5. Not checked in the game yet

- That the fisher stands in the waders and the shoes, and how the waders and the shoes sit together.
- That the game lets a custom copy of crab meat be eaten from the inventory and reports the custom item, so the meter
  fills.
- The Records tab in the game: Q and E to it and the arrows through the species in the game's browser, the board
  arriving from a real server, an angler in the game read as the race by a stranger and by name after an introduction,
  one who left read as "Stranger", and how long the reading takes on the server's own database.

## 6. Not built yet

- Fish in the satchel and on the Feed wheel: `MealEvent` and the client's food table would need to name a custom item.
- A fisher who sells crab meat, buys rods back, or keeps a stock that runs out.
- The name of an angler who is not in the game for a reader introduced to them: the reading would have to bring who
  knows each angler on the board. The race of one who is not in the game: the database does not hold it.
- Records by weight, of trophies, of a season, or more than five of a species.
