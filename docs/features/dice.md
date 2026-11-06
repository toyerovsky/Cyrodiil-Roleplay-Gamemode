# Dice (RpRoll in the core)

Branch `2026-11-06-playtest`, commit `fac4ec4` "Bring RpRoll's dice into the gamemode", on top of master `7b0d63e`.

## 1. What it is

Server-rolled dice for roleplay: d6, d10, d20 and d100, up to 5 dice per roll, announced to the players around in the
emote range and logged in the database. A 3D dice board (three.js) lets the player pick and throw dice with the mouse or
the keys, but the board only says how many of each die: every number comes from the server.

Origin: RpRoll 0.8.5 was Pete's standalone OB:MP mod. It is ported into the core so it can use the gamemode's own G and
H wheels, the F10 menu and the `/admin` panel, which a separate mod cannot reach. It replaces the old
`ChatCommands.Roll` and its `chat.roll*` texts.

## 2. Rules (as the player sees them)

- `/roll` with nothing is 1d20. `/roll 20` is also 1d20 (the old "number of sides" form still works).
- Notation: `d20`, `2d6`, `1d20+2d6`. Only the four board dice (6, 10, 20, 100). At most 5 dice per roll.
- Modifiers (`1d20+3`) are refused with their own message: "Modifiers like +3 are not rolled yet."
- The d100 has ten faces and lands on 10, 20, ... 100.
- Everyone in the emote range (1500 units by default) reads the line: `* <name> rolls 1d20 + 2d6: 14, 3, 5 = 22.` The name
  is the roller as each reader sees them (race for strangers, see `lore.md`).
- With the board open, `/roll` throws on the board instead, so the roll is made and shown once.
- Board throws have a 1.5 s cooldown ("The dice are still rolling.").
- A muted player cannot roll (`/roll` speaks; board throws check the same speech refusal).
- Dice sets are only a look: 12 sets (altmer, argonian, ayleid, bosmer, breton, daedric, dunmer, imperial, khajiit, nord,
  orc, redguard), any player may take any set, default `imperial`. The choice is saved on the character.
- "Roll against" (H wheel): both players roll a d20, the one asked (the target) wins a draw. Reach is the say range.

## 3. Ways in

| Way in | What it does | Code |
|---|---|---|
| `/roll [dice]` | Roll for those around, or throw on the board when it is open | `Dice/DiceCommands.cs` |
| `/board` | Open or close the board | `Dice/DiceCommands.cs` -> `DiceBoards.Toggle` |
| `/dice [set]` | Show your set and the list, or pick a set (`/dice nord`) | `Dice/DiceCommands.cs` |
| Home key | Open or close the board (polled bind, ignored while chat, another input page or the creator is open) | `Client/Dice/DiceOverlay.cs` |
| Board, mouse | +/- per die, Throw, set picker, light/dark, mute, close | `ui/dice/App.vue` |
| Board, keys | Space throw, 1-4 add d6/d10/d20/d100, 5 clear, Up/Down pick a die, Left/Right remove/add, V light/dark, M mute, Esc close | `ui/dice/App.vue` |
| G wheel | "Dice" (`self.roll`, Order 20): opens the board | `DiceCommands.cs` |
| H wheel | "Roll against" (`player.contest`, Order 25, `Speaks`, reach = say range) | `DiceCommands.cs`, `Rolls.Contest` |
| F10 | Tab `dice`: set picker, "Open the board" (closes the menu), last 10 rolls. Keys tab lists Home | `ui/menu/DiceTab.vue` |
| `/admin` | Tab `dice`: the server's roll log, 50 per page, filter by character name | `ui/admin/DiceTab.vue` |

The `/admin` dice tab is added for everyone the panel opens for (no extra permission), like the audit
(`AdminActions.cs`: "As the audit, the roll log is for everyone the panel opens for.").

## 4. How it works

Flow of a board throw:

1. Home / G wheel / F10 / `/board` -> server `DiceBoards.Open` (needs a loaded character in play) -> `DiceState(json)`
   with a `DiceView` (set, sets, recent rolls, max dice). The client opens the `dice` page as an input window.
2. The page sends `ThrowDice(requestId, d6, d10, d20, d100)`: counts only.
3. `DiceBoards.OnThrow` checks the pick (1 to 5 dice), the speech refusal (mute), the 1.5 s cooldown, then calls
   `Rolls.Roll`.
4. `Rolls.Roll` rolls with `DiceRoller` (crypto RNG), sends the chat line around the roller
   (`config.Chat.Emote`, `ChatLineKind.Attempt`), logs the row, remembers it in the last 10.
5. The server answers `DiceThrown(requestId, values)` in board order (d6s, d10s, d20s, d100s), and the board lands the
   dice on those values. A refusal comes back as `DiceRefused(requestId, text)`, shown on the board or, if it closed
   meanwhile, as a game message.

`/roll` without the board goes straight to `Rolls.Roll`. With the board open it sends `ThrowOnBoard(...)` (all zeros means
"the tray as it is"), and the board's own throw comes back through step 2.

Rolls made for something else (the lore card's trait 5d20) call `Rolls.Roll(player, groups, reason)` and then
`DiceBoards.Present`, which sends `PresentDice(reason, counts, values)`: a small board without input lands the dice, or the
open board does. Its layer is above the lore card.

Server-authoritative: numbers, cooldown, mute, dice count, set validity, the log. The page can only ask.

### RPCs (`src/CyrodiilRP.Common/RpContracts.cs`)

| Direction | Message |
|---|---|
| C->S | `OpenDice()`, `CloseDice()`, `ThrowDice(int requestId, int d6, int d10, int d20, int d100)`, `ChooseDiceSet(string set, bool fromMenu)` |
| S->C | `DiceState(string json)`, `CloseDiceBoard()`, `DiceThrown(int requestId, int[] values)`, `DiceRefused(int requestId, string text)`, `ThrowOnBoard(int d6, int d10, int d20, int d100)`, `PresentDice(string reason, int d6, int d10, int d20, int d100, int[] values)` |

CEF page `dice`: the page calls `sendDiceReady`, `sendDiceClose`, `sendDiceThrow`, `sendDiceSet`, `sendDicePresented`. The
F10 menu page calls `sendMenuDice` and `sendMenuDiceSet`. `/admin` uses the existing `AdminAsk("rolls", filterJson)` /
`AdminPart`.

## 5. Implementation map

| Area | Path | What |
|---|---|---|
| Server | `src/CyrodiilRP.Server/Dice/Rolls.cs` | `[Service]`, the one place that rolls; chat line, log, last 10 per character, sets cache, `Contest` |
| Server | `src/CyrodiilRP.Server/Dice/DiceBoards.cs` | `[Service]`, open boards, throw RPC, 1.5 s cooldown, `ThrowOn`, `Present` |
| Server | `src/CyrodiilRP.Server/Dice/DiceCommands.cs` | `/roll`, `/board`, `/dice`, G and H wheel options |
| Server | `src/CyrodiilRP.Server/Dice/RollNotation.cs` | Parser, `MaxDice = 5`, refusals |
| Server | `src/CyrodiilRP.Server/Dice/DiceRoller.cs`, `DieKind.cs` | RNG, d100 as 10..100 |
| Server | `src/CyrodiilRP.Server/Dice/DicePick.cs`, `DiceGroup.cs`, `RollResult.cs`, `RolledDie.cs`, `DiceFormat.cs`, `DiceSets.cs` | Value types, formatting ("20:14,6:3"), the 12 sets |
| Server | `src/CyrodiilRP.Server/Dice/DiceLog.cs` | DB writes through `Persistence.Writer`, page reads for `/admin` (50 a page) |
| Server | `src/CyrodiilRP.Server/Admin/AdminRollFilter.cs`, `AdminRollView.cs`, `AdminRollsView.cs`, `AdminParts.Rolls`, `AdminTabs.Dice` | Admin tab plumbing |
| Server | `src/CyrodiilRP.Server/PlayerMenu/MenuTabs.cs`, `MenuView.cs`, `PlayerMenus.cs` | F10 `dice` tab |
| Client | `src/CyrodiilRP.Client/Dice/DiceOverlay.cs` | `dice` page as `PageWindow`, Home bind, present mode |
| Client | `src/CyrodiilRP.Client/PlayerMenu/PlayerMenuOverlay.cs` | `OnMenuDice`, `OnMenuDiceSet` |
| Data | `src/CyrodiilRP.Data/Entities/DiceRoll.cs`, `Dice/DiceBook.cs` | Entity, write, load set + recent, page with ILIKE name filter |
| UI | `ui/dice/` | Vue page around RpRoll's tray: `App.vue`, `board/` (`scene.js`, `physics.js`, `tray.js`, `dice.js`, `models.js`, `sets.js`, `sound.js`), `notation.js` |
| UI | `ui/menu/DiceTab.vue`, `ui/admin/DiceTab.vue`, `ui/admin/dice.css` | F10 and `/admin` tabs |
| UI build | `ui/scripts/dice-models.mjs`, `ui/vite.config.js`, `ui/scripts/check-dist.mjs` | Models as scripts, dice files under `dist/dice/`, separate size budget |
| Art | `ui/rp/art/icons/dice.webp`, `ui/rp/art/d20.webp`, `ui/dice/models/icons/*.webp` | Tab medallion (Pete's d20), per-set die icons |

**Database** (migration `20261105203008_Dice`):

- New table `dice_rolls`: `id` (identity), `at`, `character_id` (FK `characters`, cascade), `character_name` varchar(81),
  `asked` varchar(64), `dice` varchar(64), `total` (check `total > 0`), `reason` varchar(64) null, `area` varchar(256) null.
  Index `(character_id, id)`.
- New column `characters.dice_set` varchar(32) null (null = default set).

**Config:** none. Chat range comes from the existing `chat.emote` (rolls) and `chat.say` (contest reach).

**Permissions:** none new. The `/admin` dice tab shows for every panel holder.

**Locales:** `locales/{en,pl}/dice.json` (generated `DiceText`), `locales/{en,pl}/ui.dice.json`, plus edits to `chat.json`
(old roll texts removed), `ui.admin.json`, `ui.menu.json`.

**CEF page and layer:** `dice`, `WebPages.DiceLayer`. Layer 4 in `fac4ec4`, 5 on the playtest branch (under the wheel, over
the lore card), 6 on the Sanctuary branch.

**New npm dependency:** `three` 0.185.1 (`ui/package.json`). A deploy must run `npm ci` in `ui/` again, since `pack.ps1`
only runs it when `ui/node_modules` is missing.

## 6. Design decisions and why

- **Crypto RNG.** `DiceRoller.RollDie` uses `RandomNumberGenerator.GetInt32`: "even, and nothing in earlier rolls tells
  the next".
- **d100 is 10..100.** The board's d100 is a ten-faced die; the value is `10 * (1..10)`, kept from RpRoll.
- **The server picks every number.** The board sends counts, never values. The same request id ties a throw to its
  answer, so a late answer cannot land on another throw.
- **One roll, shown once.** `/roll` with the board open throws on the board instead of rolling twice.
- **5 dice max:** "as many as the board lays out and the eye still reads".
- **1.5 s cooldown:** about as long as a throw takes to settle.
- **Log row keeps the name as it was then** (`character_name`), so the staff can read old rolls after a rename. The name in
  the log is the real one; the chat line is resolved per reader.
- **Present board on its own layer above the lore card,** so a trait roll is visible while the card stays open.
- **Dice files counted apart.** three.js and the 12 sets of textures are about 5.5 MB, so `check-dist.mjs` gives `dist/dice/`
  its own 6144 KB budget and keeps the 1024 KB budget for the other pages.
- **Models as scripts** (`DICE_MODEL(name, payload)`): a page served from `readym://` can load a script but not fetch a file.

## 7. Tested / not tested

Automated (counted from the commit):

- xUnit: 40 cases (12 facts, 6 theories with 28 rows) in `tests/CyrodiilRP.Tests/Dice/` (`RollNotationTests`,
  `DiceRollerTests`, `DicePickTests`, `DiceFormatTests`) and `tests/CyrodiilRP.Tests/Database/DiceBookTests.cs`
  (real PostgreSQL). `Admin/AdminRulesTests.cs` updated for the new tab.
- Vitest: 15 hand-written (`ui/tests/dice.test.js` 12, `admin.test.js` 1, `menu.test.js` 2), plus the page contract
  tests generated from `ui/pages.js`.
- Playwright: 7 (`ui/e2e/dice.spec.js` 5, `admin.spec.js` 1, `menu.spec.js` 1), run at 4 sizes.
- Suite on the playtest branch (`pack.ps1 -Test`): 338 Vitest, 499 xUnit, packed mod on the real server 2/2.

In game: Pete tested alone and it was fine: the board, `/roll`, the F10 and `/admin` dice tabs, the G wheel.

Not tested (needs 2 players):

- "Roll against" on the H wheel: both roll, both read the result.
- Other players reading your `/roll` line in chat.
- The `/admin` roll log with rolls of several players.

## 8. Known gaps and next steps

- **G wheel cap.** `WheelOptions.MaxOptions = 8` and `BuildItems` stops at 8. With Dice (20) and Lore card (25) a staff
  member has 9 self options, so "Staff panel" (`self.admin`, Order 105) is cut. In a party, "Leave the party" (50) also
  comes in and "Staff chat" (100) is cut too. Needs a decision from Przemysław: a second page, or more slots.
- **H wheel cap, same limit (found in the code, not in the plans).** Dice adds "Roll against" (25), the lore card adds
  "Read card" (11) and "Introduce yourself" (12, hidden once both know each other). With Greet, Private message, Trade and
  Invite to party, a player already has 7 H options. A staff member with `aheal` reaches 8 at "Heal" (100), so "Staff
  card" (105), "Mute" (120) and "Kick" (130) are cut. A party leader looking at a member can lose "Hand over the lead"
  (42). Same decision as above.
- **Modifiers** are refused "not rolled yet": not built.
- **"Roll against" has no cooldown of its own** (the board's 1.5 s applies to board throws only; `/roll` goes through the
  chat's flood limit).
- **Page bundle:** the non-dice pages are at 1013 KB of the 1024 KB budget on the Sanctuary branch (all three features).
