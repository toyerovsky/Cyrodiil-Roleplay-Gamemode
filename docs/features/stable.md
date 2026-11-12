# Stable

## 1. What it is

A stablemaster NPC sells the game's five horses. A character owns one, calls it from the G wheel out in the open world,
and is the only one who rides it.

## 2. Rules (as the player sees them)

- H over a stablemaster opens the stable's window: the five horses (Paint, Bay, Chestnut, White, Black) with their
  prices, the character's horse if any, the gold carried, and Buy. The price is taken from the gold carried.
- One horse a character. Buying another replaces it, and nothing is paid back: the window says so above the list while
  the character owns one, and asks before such a purchase (Replace it, or Keep my horse). The horse owned is not sold
  again.
- The window closes when the player walks more than 5 m from the stablemaster, leaves the world, or the stablemaster is
  taken away.
- "Call your horse" on the G wheel, shown only to a character who owns one, stands it about 2 m to the character's side.
  One horse in the world a player: calling again takes the one before away, dead or alive, and stands a new one. A dead
  horse can be called again at once.
- The horse comes only in the open world (`L_Tamriel`). In an interior or a city the option shows closed with the reason,
  and so it does while the character rides it or just after a far teleport.
- Only its owner rides it. Anyone else's mount is refused, with a notice in the corner.
- The horse leaves the world when its owner leaves the game. A horse the server brought back after a restart goes as
  soon as it is seen.
- A stablemaster is labelled "Stablemaster" over the head and, as a trader, cannot be killed for good.

## 3. Ways in

| Way in | What it does | Code |
|---|---|---|
| `/anpc here stable` | Staff: stands a stablemaster where the staff member stands | `Traders/TraderCommands.cs` |
| `/anpc remove`, `list` | Staff: takes the stablemaster in front away, lists stablemasters among the traders | `Traders/TraderCommands.cs` |
| H over a stablemaster | Opens the stable's window | `Stable/Stables.cs` |
| The window | Buy, Replace it or Keep my horse, Esc or Leave | `ui/stable`, `Stable/StableOverlay.cs` (client) |
| G wheel | "Call your horse" (`self.horse`, Order 30, icon `horse`) | `Stable/Horses.cs` |

There is no player command. The staff's panel lists a stablemaster on its World tab as a trader of the shop `stable`.

## 4. How it works

- **The NPC** is a trader spot whose shop is `stable` (`TraderRules.StableShop`, `TraderSpotKind.Stablemaster`): the same
  row in `trader_spots`, stood, kept, adopted and healed by `TraderNpcs`, under an origin of its own
  (`TraderOrigin.StablemasterSpawnerRefId`) by which every client labels it (`TraderMarkers`), in the clothes of Petrine
  of Bruma's stables. No shop of `stocks.json` may be named `stable`: loading throws.
- **The horses** are the game's own of each coat, named constants in `StableRules` (`HorsePaint` 0001F11E, `HorseBay`
  0001F11D, `HorseChestnut` 00015B92, `HorseWhite` 0001F11B, `HorseBlack` 0001F11A): no script and no owner. A called
  horse is a person of the character's cell, made as a trader is, marked by `StableRules.HorseSpawnerRefId`.
- **Prices**: `stable` in `server/config.json`, by default what the game's own stables ask: Paint 500, Bay 1000, Chestnut
  2500, White 4000, Black 5000. 0 gives a horse away; a price below 0 stops the mod at start.
- **Database**: `character_horses` (key `character_id`, the horse by name, the price paid and when). `StableBook` reads it
  and writes a purchase over the horse before it.
- **In memory** (`Horses`): the horse each player online owns, read at the join and again as the window opens, on the
  writer so a read and a purchase come back in order, and the one standing for them.
- **Gold is never lost to a failed write** (`Stables`): the price leaves the purse first, then the purchase is written.
  If the write fails, the gold goes back to the purse. A horse that replaced another takes the old one out of the world.
- **Riding**: a handler of the platform's mount request refuses it (as occupied) unless the rider is the player online
  who called that horse. Horses that are not ours are left to the platform.
- **Leaving**: the player's horse is deleted as they leave. Every 5 s (`horses.sweep`) any horse of ours no player online
  called is deleted, for the cells' saves bring them back after a restart.
- **The window** gets `StableState` (a `StableView`: each horse with its price and whether it is the one owned, and the
  carried gold) as it opens and when either changes, and `StableAnswer` after each purchase. An answer that comes after
  the window closed shows as a notice in the corner.
- **Audit**: `economy.horse_buy`, with the horse, the price and the horse it replaced.

## 5. Not built yet

- Stabling a horse at a stablemaster, a horse that follows its owner, a saddlebag.
- Selling a horse back, or any refund.
- A horse in a city's world space: the game keeps horses outside the walls, and so does the mode.
