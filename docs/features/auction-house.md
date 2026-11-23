# Auction house

## 1. What it is

Players put up the items they carry for others to bid on or buy now. An auctioneer NPC runs the house, and every
auctioneer shows the same lots across Cyrodiil. What is bought, won, unsold or owed waits at the house until its owner
collects it at any auctioneer. Sellers stay unnamed to the players.

## 2. Rules (as the player sees them)

- The "Services" topic in the game's dialogue with an auctioneer opens the window: the purse, and four tabs.
  - **Browse:** the open lots, the soonest to end first, 20 a page, by part of the name and by kind (weapons, armour,
    clothing, potions, ingredients, books, other). A lot shows its item and count, a "used" mark for a worn down one,
    its top bid or starting price, its buy now price and the time left. Picking one shows a bid field filled with the
    least bid it takes, and Buy now.
  - **Sell:** what the character carries (not what they wear, nor what players made), how many to put up, a starting
    price, a buy now price, how long (12, 24 or 48 hours), the fee and the house's cut.
  - **My lots:** the character's open lots, Take back on those nobody bid on, and the bids of theirs that lead.
  - **Collect:** what waits for them, one by one or everything at once.
- A lot asks a starting price, a buy now price or both, whole gold from 1 to `economy.maxPayment`. The buy now price is
  above the starting price. A lot with only a buy now price is not bid on, one with only a starting price is not
  bought now.
- Putting a lot up costs `auction.listingFeePercent` (2) of its highest price, rounded up, at least 1 gold, from the
  purse and kept whatever becomes of the lot. A character has at most `auction.maxLots` (10) open lots.
- The first bid is at least the starting price, each next one at least `auction.bidStepPercent` (5) over the top bid,
  and at least 1 gold more. A bid at the buy now price or above buys it now instead. A bid leaves the purse at once and
  waits at the house: when someone outbids it, it waits for its bidder to collect. The leader cannot bid again, nor the
  seller on their own lot.
- A bid in the last `auction.extendMinutes` (5) moves the end to that long after it.
- Buying now pays the buy now price from the purse and ends the lot. A top bid it ends waits for its bidder.
- When the time runs out, the top bid wins: the item waits for the winner, the price less the house's cut
  (`auction.cutPercent`, 5, rounded down) for the seller. A lot nobody bid on goes back to its seller.
- The seller takes back a lot nobody bid on. The fee is not returned.
- A notice in the corner tells a character who is in the game that they were outbid, sold, won or got nothing.
- The window closes when the player walks more than 5 m from the auctioneer, leaves the world, or the auctioneer is
  taken away. An auctioneer is labelled "Auctioneer" over the head and on the crosshair and takes no harm.

## 3. Ways in

| Way in | What it does | Code |
|---|---|---|
| `/anpc here auctioneer` | Staff: stands an auctioneer where the staff member stands | `Traders/TraderCommands.cs` |
| `/anpc remove`, `list` | Staff: takes the auctioneer in front away, lists them among the traders | `Traders/TraderCommands.cs` |
| The game's dialogue with an auctioneer, topic "Services" | Opens the window | `Auction/AuctionHouse.cs`, `docs/features/npc-dialogue.md` |
| The window | Search, bid, buy now, put up, take back, collect, Esc or Leave | `ui/auction`, `Auction/AuctionOverlay.cs` (client) |
| `/aauction list [name]` | Administrator: the open lots with their sellers, by part of the item's or seller's name | `Auction/AuctionCommands.cs` |
| `/aauction remove <lot> <reason>` | Administrator: takes a lot down, the item back to its seller and the top bid to its bidder | `Auction/AuctionCommands.cs` |

## 4. How it works

- **The NPC** is a trader spot whose shop is `auction` (`TraderRules.AuctionShop`, `TraderSpotKind.Auctioneer`),
  stood, kept and kept from harm by `TraderNpcs` with its origin `TraderOrigin.AuctioneerSpawnerRefId`. No shop of
  `stocks.json` may be named `auction`.
- **Database**: `auction_lots` (the item as the game keeps it: form, count, durability and charge, or for a custom
  item a mod defined form 0 and its key, `custom_item`, as its form lasts one run only; its name and record type for
  the search, the seller's name as it was for the staff, the prices, the fee, the top bid and
  its bidder, the status `Open`, `Sold`, `Expired`, `Withdrawn` or `Removed`, the buyer, the price and the cut) and
  `auction_parcels` (what waits for a character: the lot's item, or gold for a sale, an outbid and a refund).
  Constraints keep a lot's prices, bids and status consistent, a lot either a game item or a custom one, and a gold
  parcel above 0. A custom item no mod defines when its parcel is collected or handed back waits in Collect, logged as
  an error, until one defines it again.
- **Every change on the writer** (`AuctionBook`): a post counts the seller's open lots, a bid, a purchase, a take back,
  a staff removal and the closing of lots that ran out each read the lot and write it with the parcels it owes in one
  save. So two bids never both win and a parcel is never owed twice. The shared arithmetic (fee, cut, least bid, the
  end moved by a late bid) is `AuctionRules`.
- **Nothing in two places** (`AuctionHouse`): the item and the fee leave the character before the lot is written, and
  a bid or a purchase leaves the purse before it is written. Whatever the house refuses or could not write is handed
  back. What the character cannot take back at that moment (they left, or a load holds them) is kept for them: the
  gold as a delivery that comes once they are in the world (`GoldDeliveries`), the item of a lot not written as a lot
  taken back that waits in Collect. A parcel is claimed in the database before it is handed over, and released when
  it could not be, as gold deliveries are. If the server stops between the two halves, the item or gold is lost, never
  doubled.
- **Lots that run out** are closed every 10 seconds, also those that ended while the server was down.
- **The window** gets `AuctionOpened` as it opens and `AuctionState` (an `AuctionView`) as anyone changes the lots or
  what the character carries changes. The client lets an update in flight as the player closes the window open
  nothing. `AuctionAnswer` says what a request came to. A character sends one request at a time.
- **The time left** is sent in milliseconds from when the view was made, and the page counts it down.
- **Audit**: `auction.post`, `auction.bid`, `auction.buy`, `auction.withdraw`, `auction.collect`, `auction.settle` and
  the staff's `auction.remove` with its reason.

## 5. Not built yet

- The staff panel's tab for the house. The staff use `/aauction`.
- Items players made in the game (potions, enchanted gear), which nothing names, and anything worn. The custom items
  the mods define (`ICustomItems`) are sold like the game's, sorted by the kind of the item each copies.
- Paying with gold at the bank.
- Not checked in the game: the window at a real auctioneer, the items handed back with their durability and charge,
  and the crosshair's name.
