# Guides

## 1. What it is

A card over the whole screen the first time a player meets one of the mode's windows: what it is for, a few steps,
the keys they are done with, the numbers worth knowing and a picture. It comes once an account, so a second character
of the same player does not see it again, and the Help tab of the player's menu (F10) opens any of them again. The
first entry into the world gets a welcome card with the mode's keys.

## 2. Rules (as the player sees them)

- **When.** The first time the player opens the trader's shop, the bank, the stable, a mentor's or the registrar's
  window, a Jobs foreman's window, a notice board, the gaming table, the dice board, their own lore card (End), or
  offers a trade from the H wheel. The window waits behind the card.
- **Continue** (E, Enter or the button) closes the card and opens the window, as if the card had not been there.
  **Close** (Esc) only closes it: the next use opens the window at once. The card waits 0.6 s before it takes E or
  Enter, so the hand still on the key that opened the window does not skip it.
- **The welcome** comes 3 s after the player first takes a character into the world (Play, or the end of the
  character creator): G, H, K, Y and N, F10, Home, End, each with a line, and the topic "Services" of the mode's people.
  It only closes.
- **Once an account.** A guide shown is never shown on its own again, whichever of the account's characters plays.
  The Help tab lists every guide with its picture, read or still ahead, and opens it again over the menu. Closing it
  brings the menu back on the Help tab.
- **The numbers** on a card come from the rules and `server/config.json` the server runs (prices of the horses and of a
  specialization, the board's limits, the stakes), so a card never says other than the window does.
- A card that another window takes the place of (a trade accepted by the other player, say) closes, and nothing
  follows it: the window behind it does not open, and a guide read again does not bring the menu back over the new one.

| Guide | Comes before | Facts it shows |
|---|---|---|
| Welcome to Cyrodiil | the first entry into the world | none, the mode's keys instead |
| The trader | the shop | pieces in one purchase, the walk-away distance |
| The bank | the bank's window | gold moved at once, the fee (none) |
| The stable | the stable's window | the cheapest and the dearest horse |
| The mentor | a mentor's or the registrar's window | the first choice, a change, the wait between two |
| The foreman | a Jobs foreman's window | none |
| The notice board | the board's window | notices a character keeps up, the wait between two, how long one stays, how many a board shows |
| The gaming table | the table's window | the stake, rounds to win, time to answer, the house's cut |
| The dice board | the board | dice in one throw, dice sets |
| The lore card | the player's own card | time to answer an introduction |
| The trade | the offer to trade | gold in one trade, time to answer |

## 3. Ways in

| Way in | What it does | Code |
|---|---|---|
| The first use of a window | Shows its guide, the window waits for Continue | `Guide/Guides.cs` (`ShowFirst`), called by each window's service |
| The first entry into the world | The welcome card | `Guides`, on `OnlinePlayers.Playing` |
| F10, Help tab, a guide | Reads it again, then back to the menu | `ui/menu/HelpTab.vue`, `ReadGuide`, `PlayerMenus` |
| The card | E or Enter goes on, Esc closes | `ui/guides`, `Guide/GuideOverlay.cs` (client) |

There is no command.

## 4. How it works

- **Database**: `account_guides` (key `account_id` and `topic`, the `GuideTopic` by name, and when it was first seen).
  `GuideBook` reads an account's guides and marks one, keeping the first time.
- **In memory** (`SeenGuides`): the guides each account online has seen, read as the account loads. Until then a
  window opens without its guide, so no use ever waits on the database. A guide is marked in memory as it shows and
  written after: a failed write only shows it again at the next join.
- **The card** (`GuideCatalogue`): every topic's texts (`locales/*/guides.json`, `GuidesText`) with its numbers,
  built from `RpConfig` and the rules' constants. Numbers of five figures and more are grouped by threes with a
  no-break space. `Guides` puts it in the reader's language and sends it as a `GuideView` (`Guide`). A player has one
  card at a time, and while one is up no other comes.
- **What waits**: `Guides.ShowFirst(player, topic, use)` runs `use` at once, or keeps it until the card's Continue
  (`GuideContinue`). Each window checks again as it opens (the person near, the board standing, the other player still
  in reach for a trade), as it does for any request.
- **The page** (`ui/guides`): a film's opening over the darkened game, the bands at the top and the bottom, the title,
  the steps one after another and the picture rising out of the dark. Its layer is over every window
  (`WebPages.GuideLayer`). The client holds a card that came before the character's pawn settled until it has.
- **Pictures**: sepia ink sketches made for the mode in Recraft, kept in `design/recraft/guides` and shown from
  `ui/rp/art/guides` (`design/README.md`). `ui/guides/pictures.js` names one for every topic, a test ties it to
  `GuideTopic`.

## 5. Not built yet

- A guide for the auction house, the party and the factions.
- A step-by-step tutorial with goals and markers, as kcdrp has: the cards only explain a window as it is met.
