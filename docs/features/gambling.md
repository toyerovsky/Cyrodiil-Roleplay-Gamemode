# Gambling

Status: v2 in development on `2026-11-20-gambling-v2`, based on stage 1 (`2026-11-19-gambling`). See section 9 for the shared 3D table and its verification. Stage 2 (a dealer
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

1. Each round the challenger rolls five d6, then the opponent rolls five d6 on the shared table.
2. **Raise:** after the first roll of a round, either player may raise once in that round, by 1 to the original stake.
   The other calls (pays the same) or folds. A fold loses the match and the whole pot to the other player. A player who
   cannot pay the call has to fold or ask for less, so a raise is capped by what the other carries.
3. **Reroll:** both pick any of their five dice (none to all) and confirm. When both confirmed or the time ran out,
   the server rerolls the picked dice in seat order and shows both hands. Kept dice stay where they landed.
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
- A match decided by its last throw stays decided: leaving while the dice still roll changes nothing.
- A muted or frozen player cannot start or accept a game. A player muted during a match plays it on. A freeze during a
  match ends it as a cancelled one, with everyone's gold back, since a frozen player cannot reach the window.
- After an invitation the same player waits 10 s before the next one.
- A staff member can cancel a game (`/agamble cancel`), which gives everyone back what they put in.

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
| Dice | `src/CyrodiilRP.Server/Dice/Rolls.cs`, `DiceBoards.cs` | Every new die goes through `Rolls.Roll` with reason `gambling:<id>`, then a shared `GamblingThrow` in the roller's set |
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

Audit: `gamble.start`, `gamble.raise`, `gamble.call`, `gamble.fold`, `gamble.settle`, `gamble.refund`, `gamble.cancel`, `table.place`,
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

- **Tests:** `tests/CyrodiilRP.Tests/Gambling/` (the hands over all 7776 sets of five d6, both games' matches, escrow,
  the throws and their timing, the settings), `Database/GamblingBookTests.cs` on PostgreSQL, `ui/tests/gambling*.test.js`
  and `ui/e2e/gambling.spec.js` at the four sizes.
- **Not tested in game:** two real inventories and players, reach/world/disconnect forfeits, Y/N focus handoff, small
  board layering, game saves, delivery after reconnect and a live server restart.

## 8. Known gaps and next steps

- **Stage 2, the dealer:** `/anpc here dealer`, H over it opens the table window with the house as the opponent, both
  games, standing. The house plays Dice Poker by a fixed rule (keeps its groups, rerolls the rest), never raises, and
  wins ties in Zenithar's Favor, so the house keeps an edge and the gold lost to it leaves the economy.
- **Stage 3, the table's look:** a prop on the gaming table (a dice cup, coins) through `ILocalProps`, as the notice
  board's mesh, once the game's asset paths are listed.
- **Own tables anywhere:** needs the SDK to place furniture a character can sit on.
- **Later games:** liar's dice with hidden dice, cheating with Security against the other's notice.

## 9. v2: the shared animated table (20 November 2026)

The gambling window now uses two instances of the existing three.js board. The reader's own board is always above the
opponent's. Board identity follows the absolute seat: challenger 0 uses `board.glb`, invited player 1 uses
`board-negative.glb`, including when seat 1 is the reader. Each throw uses `Rolls.FindSet` for that character's F10 or
`/dice` choice. Poker has five d6 per board and Favor has two. The actions, pot, timer, names, rank or total and round
wins sit outside the canvases. G/H, commands, invitation cards, rules, history and staff cancellation keep their existing
entry points. Esc releases the window without ending the match.

### Events and names

The existing `GamblingState` channel carries the event history under `match.throws`. An event has this shape:

```json
{
  "id": 3, "match": 81, "seat": 0, "round": 1, "replay": 0, "kind": "Reroll",
  "indices": [0, 4], "values": [6, 3], "seed": 197361, "set": "imperial"
}
```

Indices are zero based and values correspond to those indices. Event ids increase within one match. `Initial` events
include every die. `Reroll` events include only the selected dice; keeping all dice produces no throw event. The seed is
chosen by `RandomNumberGenerator`, separately from the values rolled and logged by `Rolls`. The same event objects are
projected to both readers, without names or relative seats in them. Future throws are neither rolled nor published
until their seat's turn begins, including their chat announcements.

The envelope adds `seat`, `turnSeat`, `picks` (two absolute-seat masks), `activeThrow`, `throwElapsedMs`, `animationMs`
and `roundWinner` (an absolute seat or -1). Existing `wins` and `winner` keep their reader-relative meaning. Names stay
in `you` and `opponent`: the reader's own character name, and `ShownNames.Shown(reader, opponent)` for the other seat.
Rolling, turn, call, round and match banners use those reader-specific names. The page never chooses a die value.

### Timing and decisions

`GamblingMatch.RollSeat` and `RerollSeat` enforce seat 0 then seat 1. A published throw holds the server's animation
gate for **3000 ms**. Only after it expires may the next seat roll. The 100 ms watcher can extend a throw slightly;
a late tick gives the next seat its full 3000 ms. Persistence can also extend the wait. Actions are refused while the
gate or a persistence operation is active. A seat keeping every die is skipped without an empty animation.

After the last actual throw lands, the full configured decision countdown starts (45 seconds by default). Favor
settles or shows a tied round only after its second throw. Poker scores only after both seats have processed their
rerolls. A nonterminal round result remains for **4000 ms**, then the next round or replay starts. Wins and result
banners are withheld during the final animation. `GamblingTiming.Deadline` rejects starting a decision while the
animation gate is active.

`GamblingAct(..., "Select", mask)` relays an unconfirmed live selection. The server checks membership, decision
version, phase, confirmation, deadline and the five-bit mask. `GamblingPicks` coalesces updates to at most one relay
per 100 ms per match, with the latest state retained for both seats. `Pick` remains the confirmation action and carries
the final mask directly. A confirmed seat cannot select again; timeout still keeps unconfirmed dice. The page keeps
its latest optimistic selection until acknowledged, so an older relay cannot undo a fast second key press.

### Motion and reopening

The existing `planThrow` has a CPU-time search budget. Shared throws use `planPartialThrow` with **eight fixed
attempts** instead, for both initial and partial throws, so two machines choose the same trajectory for a seed.
`finishPartialThrow` applies `finishThrow` only to moving dice and preserves each kept position and quaternion exactly.
Kept dice are bodies of zero inverse mass and inertia, using the existing sphere contact approximation as obstacles.
The existing full-throw API and its time-budgeted planner remain available to `/roll` and the separate dice page.
Playback fits the chosen trajectory into the server's 3000 ms window, accounting for time spent loading and planning.

The page reconstructs each seat from its current round's initial event followed by its rerolls. A reopened snapshot
jumps to those tracks' final poses without sound or replay. New live events animate once. Selection relays do not
restart animation. Click raycasts, the buttons below the boards and CEF number keys 1-5 all toggle the same mask;
Space or Enter confirms. Selected meshes lift and glow. Render loops stop when the dice stop; closing the table
releases both renderers and their resources.

Both board models are emitted as model scripts by `dice-models.mjs` and required by `check-dist.mjs`. A single clearly
marked fallback in `dice-models.mjs` uses `board.glb` if the negative GLB is absent at build or preview startup. Remove
that fallback once the negative model is permanently shipped. The negative file arrived during this task and was
used in the model-script verification. The dice asset budget is now 7 MiB, allowing the second board; three.js and
the shared renderer remain under `dice/assets`, rather than being counted in the ordinary pages budget.

### Files changed

- Server `Gambling/`: `GamblingGames.cs`, `GamblingMatch.cs`, `GamblingMatchView.cs`, `GamblingSession.cs`,
  `GamblingAction.cs`; new `GamblingThrow.cs`, `GamblingThrowKind.cs`, `GamblingThrows.cs`, `GamblingPicks.cs`,
  `GamblingTiming.cs`.
- Page `ui/gambling/`: `App.vue`, `gambling.css`, `pretend.js`; new `TableBoard.vue`, `table.js`.
- Shared dice code `ui/dice/board/`: `physics.js`, `scene.js`, `models.js`.
- Packaging/preview: `ui/scripts/dice-models.mjs`, `ui/scripts/check-dist.mjs`, `ui/vite.config.js`, `ui/pages.js`.
- Tests: new `tests/CyrodiilRP.Tests/Gambling/GamblingThrowsTests.cs`, `ui/tests/gambling-board.test.js`,
  `ui/tests/gambling-physics.test.js`; updated `ui/tests/gambling.test.js`, `ui/e2e/gambling.spec.js`.
- Texts/docs: `locales/en/ui.gambling.json`, `locales/pl/ui.gambling.json`, this document.
- `ui/dice/models/board-negative.glb` appeared externally during the task and was verified as a packaging input;
  it was not authored or edited by this implementation.

### Still to check in the game

Still requiring the game: two actual CEF clients and network delay, mouse raycasts on OB:MP, recognition changes,
Esc/G focus handoff during a throw, the players' saved dice sets, sound, forfeits/cancellation during animation and
persistence delays. Daedric dice (dark with red pips) are the least contrasted set on the negative board's dark floor,
still readable.
