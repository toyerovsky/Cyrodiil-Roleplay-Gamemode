# Gambling

Status: in development on `2026-11-19-gambling` (stage 1: players against players at a gaming table). Stage 2 (a dealer
NPC) and stage 3 (a prop on the table) are described in section 8 and not built yet.

## 1. What it is

Dice games for gold between two characters at a gaming table in an inn. Two games:

- **Dice Poker** (PL: Kościany poker), the default and the first in every list: five d6 each, one reroll of the dice a
  player picks, hands ranked as in poker, best of three rounds. Modelled on the traditional game of poker dice, in the
  form players know from tavern games in RPGs. Nothing (names, art) is taken from any other game.
- **Zenithar's Favor** (PL: Łaska Zenithara), the quick one: 2d6 each, the higher total takes the pot. Named after
  Zenithar, the Divine of trade and wealth.

Every die is rolled by the server through `Rolls` (crypto RNG) and logged in `dice_rolls`. The gold is the game's own
gold in the inventory (`Wallet`), held by the server in escrow while a game runs.

## 2. Rules (as the player sees them)

### A game

- Both players must be at the same gaming table: within the table's reach (`gambling.tableReach`) of the spot the staff
  marked. Sitting on the inn's chairs is roleplay; the server checks only the distance.
- The challenger picks the game (Dice Poker first), the stake and the opponent. The opponent gets an invitation card
  with the game and the stake and answers Y or N (as a party invitation) within `gambling.inviteSeconds`.
- On Y the server takes the stake from both. If it cannot take it from the second, it gives the first back and the
  game does not start ("You do not carry 40 gold."). The stakes are the pot.
- A stake is a whole number of gold from `gambling.minStake` to `gambling.maxStake`.
- One game at a time per character, and a table holds `gambling.gamesPerTable` games at once.
- The winner of the match takes the whole pot, less the house's cut (`gambling.cutPercent`, 0 by default, rounded
  down). A drawn match gives both their stakes and raises back.

### Dice Poker

1. Each round both roll five d6 at once.
2. **Raise:** after the first roll of a round, either player may raise once in that round, by 1 to the original stake.
   The other calls (pays the same) or folds. A fold loses the match and the whole pot to the other player. A player who
   cannot pay the call has to fold or ask for less, so a raise is capped by what the other carries.
3. **Reroll:** both pick any of their five dice (none to all) and confirm. When both confirmed or the time ran out,
   the server rerolls the picked dice of both at once and shows both hands.
4. The better hand wins the round. The first to win two rounds wins the match
   (`gambling.pokerRoundsToWin`, 2 by default).

Hands, from the lowest:

| Hand | Example |
|---|---|
| Nothing | 1 3 4 5 6 (not a straight) |
| Pair | 4 4 1 2 6 |
| Two pairs | 5 5 2 2 6 |
| Three of a kind | 3 3 3 1 6 |
| Small straight | 1 2 3 4 5 |
| Big straight | 2 3 4 5 6 |
| Full house | 6 6 6 2 2 |
| Four of a kind | 2 2 2 2 5 |
| Five of a kind | 4 4 4 4 4 |

Two hands of the same rank are compared by their dice in this order, the first difference decides:

1. the dice of the larger group first, by their value (three of a kind before the pair in a full house, the higher pair
   before the lower in two pairs),
2. then the remaining dice from the highest.

So a pair of sixes beats a pair of fives, and two equal pairs go to the higher fifth die. Two straights of the same
kind, or two identical sets of dice, are a tie: the round is played again. After `gambling.maxReplays` replays of one
round in a row the match is a draw.

### Zenithar's Favor

Both roll 2d6 once. The higher total takes the pot. A tie is rolled again, up to `gambling.maxReplays` times, then the
match is a draw. No raise, no reroll.

### Time, leaving and losing

- Every decision (invitation, raise, call, reroll pick) has `gambling.decisionSeconds` (45 by default). When time runs
  out: no raise, a fold on a call, and the dice kept as they are on a reroll.
- A player who leaves the table's reach, leaves the world or the server during a match loses it: the pot goes to the
  other. If both are gone, both get their stakes and raises back.
- Gold for a player who is not in the world is owed through `GoldDeliveries` and given on their next visit.
- A muted or frozen player cannot start or accept a game. A staff member can cancel a game (`/agamble cancel`), which
  gives everyone back what they put in.

## 3. Ways in

| Way in | What it does |
|---|---|
| G wheel, "Gaming table" (only within a table's reach) | Opens the table window: the games (Dice Poker on top), the stake, the players at the table to challenge |
| H wheel, "Play dice" (only when both are at the same table) | Opens the table window with the player in front as the opponent |
| `/gamble <player number> <stake> [poker\|favor]` | Challenges a player at the same table, Dice Poker when the game is left out |
| `/gamble rules` | Opens the table window on its rules page, anywhere |
| Y / N on the invitation card | Accepts or declines |
| The game window, mouse and keys | Raise, call, fold, pick dice (click or 1 to 5), confirm (Space or Enter), Esc only hides the window, the game goes on |
| F10, Dice tab | The character's last 10 games: game, opponent, stake, result |
| `/admin`, Gambling tab | The server's games, 50 a page, filter by character name, with Cancel for a running one |
| `/atable here\|remove\|list` | Administrator: marks a gaming table at the spot in front of them, takes the nearest one away, lists them |
| `/agamble list\|cancel <game>` | Moderator: the running games, cancel one |

## 4. How it works

| Area | Paths | Implementation |
|---|---|---|
| Hands and match rules | `src/CyrodiilRP.Server/Gambling/PokerHand.cs`, `PokerHandRank.cs`, `GamblingMatch.cs`, `GamblingPhase.cs`, `GamblingRound.cs`, `GamblingEscrow.cs` | Pure evaluation, two raise opportunities, simultaneous picks, round wins, replays, draw and forfeit |
| Tables | `src/CyrodiilRP.Server/Gambling/GamblingTables.cs`, `GamblingTableRegistry.cs`, `GamblingTableRecord.cs` | Database-backed spots, area and distance checks, nearest/shared table |
| Invitations and matches | `src/CyrodiilRP.Server/Gambling/GamblingGames.cs`, `GamblingSession.cs`, `GamblingSessionRegistry.cs` | Y/N invitations through `Players/Asks.cs`, decision versions and deadlines, leaving, escrow, retry and recovery |
| Commands and wheels | `src/CyrodiilRP.Server/Gambling/GamblingCommands.cs` | `/gamble`, `/atable`, `/agamble`, G Gaming table and H Play dice |
| Persistence | `src/CyrodiilRP.Data/Entities/GamblingTable.cs`, `GamblingGame.cs`, `Gambling/GamblingBook.cs` | Tables and games, names as they were, JSON rounds, idempotent creation and terminal settlement |
| Migration | `src/CyrodiilRP.Data/Migrations/20261119221231_Gambling.cs` | `gambling_tables`, `gambling_games`, constraints, indexes and role permissions: administrator `atable`, moderator `agamble` |
| Config | `src/CyrodiilRP.Server/GamblingSettings.cs`, `RpConfig.cs`, `Mod.cs`, `config.json` | Defaults below, validation at start including pot overflow |
| Dice | `src/CyrodiilRP.Server/Dice/Rolls.cs`, `DiceBoards.cs` | Every new die goes through `Rolls.Roll` with reason `gambling:<id>`, then `DiceBoards.Present` in the roller's set |
| Wire and client | `src/CyrodiilRP.Common/RpContracts.cs`, `Gambling/GamblingMessageKind.cs`, `src/CyrodiilRP.Client/Gambling/GamblingOverlay.cs` | Five RPC names, one typed server message channel for state, refusal and invitation; `PageWindow` holds input |
| Page | `ui/gambling/` | Game picker, stake, opponents, complete rules, both hands, decisions, countdown and keyboard picks |
| Invitation | `src/CyrodiilRP.Server/Players/Asks.cs`, `AskBook.cs`, `ui/asks/` | Existing Y/N card with localized game and stake, configurable invitation duration; the gambling window hides so Y/N can answer |
| History | `src/CyrodiilRP.Server/Gambling/GamblingLog.cs`, `ui/gambling/History.vue`, `ui/menu/DiceTab.vue` | Last ten games, names resolved for the reader; known offline opponents retain their historical name, offline strangers are anonymous |
| Staff panel | `src/CyrodiilRP.Server/Admin/AdminPanel.cs`, `AdminParts.cs`, `AdminTabs.cs`, `AdminActions.cs`, `ui/admin/GamblingTab.vue` | Fifty games per page, historical name filter, cancellation through `/agamble` with duty and permission checks |
| Texts | `locales/{en,pl}/gambling.json`, `ui.gambling.json`, `ui.asks.json`, `ui.admin.json` | Both languages, Poker first in game choices and rules |

Gold is taken through `Wallet.TryTake` before its game row is written. A failed second stake returns the first in the
same update tick. A raise is taken from its proposer immediately and persisted, the call is taken and persisted
separately. Decision versions and busy states prevent duplicated deductions. Failed persistence keeps the match locked
and retries the same operation, including an idempotent operation id for initial creation.

Settlement or refund changes the open game to its terminal status and creates `GoldDeliveries` in the same PostgreSQL
transaction. `GoldDeliveries` claims each payment before `Wallet.TryGive`, releasing it again if the player is away or
cannot receive it. Repeating settlement cannot create another payment. Startup refunds all open games before accepting
new invitations. A removed table is retired with `removed_at`, preserving its games' history.

Each player may propose one raise per round, including its tied replays. The challenger decides first, then the opponent;
the picks follow after those decisions. A confirmed pick is kept when the other player's time runs out, while an
unconfirmed pick keeps all dice. A round's result stays visible for four seconds before its next roll. Esc hides only the
window; G Gaming table reopens the live match.

Audit: `gamble.start`, `gamble.raise`, `gamble.fold`, `gamble.settle`, `gamble.refund`, `gamble.cancel`, `table.place`,
`table.remove`. No NPC or prop is created in stage 1.

The SDK inventory save and PostgreSQL are separate systems. Their writes cannot be committed as one transaction: a hard
process failure between taking inventory gold and its first durable game write remains a platform limitation.

## 5. Configuration (`gambling` in `server/config.json`)

| Key | Default | Meaning |
|---|---|---|
| `minStake`, `maxStake` | 1, 500 | A stake in gold |
| `cutPercent` | 0 | The house's cut of the pot, 0 to 50 |
| `tableReach` | 3 m (in the config's units) | How far from a table's spot a player is at it |
| `gamesPerTable` | 2 | Matches at one table at once |
| `inviteSeconds`, `decisionSeconds` | 30, 45 | Time to answer an invitation and to decide |
| `pokerRoundsToWin` | 2 | Rounds to win a Dice Poker match |
| `maxReplays` | 3 | Ties of one round in a row before the match is a draw |

A value that makes no rule stops the mod at start with the reason in the log.

## 6. Design decisions and why

- **Dice Poker is the default,** because it is the game players ask for: a decision after the roll (which dice to
  reroll, whether to raise) makes it roleplay, not a coin toss.
- **Existing tables and chairs.** A prop the SDK stands (`ILocalProps`) is a mesh with collision, nobody can sit on it.
  The game's own furniture can be sat on and the platform syncs sitting, so the inns' tables are used as they are and
  the staff only mark them.
- **Distance, not sitting.** The mod's SDK does not tell the server who sits where, so being at a table is a distance.
- **Leaving loses.** A match can take minutes; a player behind could otherwise leave to keep their stake.
- **Gold in the inventory,** not the bank: coins on the table, and the bank stays a safe place.
- **No skill modifiers.** Between players the game is even.

## 7. Tested / not tested

Verification on 19 November 2026:

- **Passed:** 53 xUnit cases from `tests/CyrodiilRP.Tests/Gambling/`, compiled and run against the real source files in
  a temporary project independent of the missing SDK. The exhaustive hand test enumerates all 7776 ordered five-d6
  sets and their rank counts, checks order independence, the spec examples, ties and group tie-breaks. Match tests
  cover both games, both raises, simultaneous selection, timeout defaults, folds, replays and leaving. Escrow tests cover a failed second stake, immediate compensation, and cut rounding. Config tests
  reject invalid rules and pot overflow.
- **Passed:** `dotnet build src/CyrodiilRP.Data --no-restore -m:1 /nr:false /p:UseSharedCompilation=false`, including the
  gambling persistence model; `dotnet ef migrations has-pending-model-changes --project src/CyrodiilRP.Data --no-build`
  reports no pending changes. EF generated migration `20261119221231_Gambling` without a database connection.
- **Passed:** eight direct DOM flow checks of the compiled page over jsdom: ready, Poker first, fractional-stake
  rejection, duplicate-invite prevention, mouse and CEF key selection, server refusal, Esc and Polish rules. This is
  an additional unit-level check, not a browser layout test.
- **Passed:** 19 CEF bridge contracts and four EN/PL key/placeholder contracts checked directly with Node.
- **Passed:** Vue compiler parsing, script compilation and template compilation of the eight changed/new components
  (`gambling/App.vue`, `Rules.vue`, `History.vue`, `admin/GamblingTab.vue`, `admin/App.vue`, `menu/App.vue`,
  `menu/DiceTab.vue`, `asks/App.vue`). This is a static check, not a browser test.
- **Blocked:** full `dotnet build` and the repository's xUnit suite. `.sdk/` contains only `.gitkeep`; existing restored
  public SDK assets lack the `ClientToServer` / `ServerToClient` attributes this branch uses. No SDK packages or server
  folders were changed to work around it.
- **Skipped:** PostgreSQL tests (`tests/CyrodiilRP.Tests/Database/GamblingBookTests.cs`). Docker denies access to
  `npipe:////./pipe/dockerDesktopLinuxEngine` and `127.0.0.1:5433` is unreachable. Tests are written for real transactions,
  exact refunds, idempotent creation/settlement, restart recovery, claims, constraints, filtering retired tables, and name visibility in offline history.
- **Passed, outside the sandbox:** the whole Vitest suite (515 tests, 33 files) and `npm run build` with
  `check-dist`. A decision sent after the deadline was caught here: `act` now checks the clock itself, not the
  ticker's last value.
- **Passed, outside the sandbox:** Playwright at `E2E_SIZES=all` for `gambling.spec.js`, `admin.spec.js` and
  `menu.spec.js` (112 runs). Screenshots opened and judged by eye at all four sizes: the picker, raise, reroll, result,
  Favor, rules (top and bottom), the invitation card, F10 history and the staff's tab. Nothing cut off, no horizontal
  scroll, the dice and their key numbers readable at 1366x768.
- **Not tested in game:** two real inventories and players, reach/world/disconnect forfeits, Y/N focus handoff, small
  board layering, game saves, delivery after reconnect and a live server restart. Nothing was deployed.
- **Blocked:** `./pack.ps1 -Test -PackOnly` stops at "No SDK in .../sdk", since the only server here is the public
  1.0.1. The public SDK 1.1.0 from nuget.org does not build master either: 27 errors in code that is already there
  (`Props`, `Statuses`, `Foliage`, `IAnimations`), all rp-framework API. The gambling projects never got to compile.

Stage 1 still needs the full build, the PostgreSQL tests and the server smoke tests (`./pack.ps1 -Test -PackOnly`),
which wait for a server package from rp-framework CI with its `sdk/`, before it can be called verified.

## 8. Known gaps and next steps

- **Stage 2, the dealer:** `/anpc here dealer`, H over it opens the table window with the house as the opponent, both
  games, standing. The house plays Dice Poker by a fixed rule (keeps its groups, rerolls the rest), never raises, and
  wins ties in Zenithar's Favor, so the house keeps an edge and the gold lost to it leaves the economy.
- **Stage 3, the table's look:** a prop on the gaming table (a dice cup, coins) through `ILocalProps`, as the notice
  board's mesh, once the game's asset paths are listed.
- **Own tables anywhere:** needs the SDK to place furniture a character can sit on.
- **Later games:** liar's dice with hidden dice, cheating with Security against the other's notice.
