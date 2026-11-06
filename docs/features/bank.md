# Bank

## 1. What it is

A banker NPC keeps a character's gold. Gold at the bank is a number in the database, not an item: it stays over deaths
and server restarts, and nobody takes it from the character on the road.

Trello: "Add banker NPC with working player bank accounts" (https://trello.com/c/mD6oOFZx).

## 2. Rules (as the player sees them)

- H over a banker opens the bank's window: the gold at the bank, the gold carried, an amount to type, Deposit and
  Withdraw. Two chips fill the amount in: all that is carried, all that is at the bank.
- One deposit or withdrawal moves 1 to 1,000,000 gold (`BankRules.MostAtOnce`). A larger sum takes more than one.
- An account belongs to the character. It is made by the first deposit and has no fee, no interest and no limit.
- The window closes when the player walks more than 5 m from the banker, leaves the world, or the banker is taken away.
- A banker is labelled "Banker" over the head and, as a trader, cannot be killed for good.

## 3. Ways in

| Way in | What it does | Code |
|---|---|---|
| `/anpc here banker` | Staff: stands a banker where the staff member stands | `Traders/TraderCommands.cs` |
| `/anpc remove`, `list` | Staff: takes the banker in front away, lists bankers among the traders | `Traders/TraderCommands.cs` |
| H over a banker | Opens the bank's window | `Bank/Bankers.cs` |
| The window | Deposit, withdraw, Esc or Leave | `ui/bank`, `Bank/BankOverlay.cs` (client) |

There is no player command. The staff's panel lists a banker on its World tab as a trader of the shop `bank`.

## 4. How it works

- **The NPC** is a trader spot whose shop is `bank` (`TraderRules.BankShop`, `TraderSpotKind.Banker`): the same row in
  `trader_spots`, stood, kept, adopted and healed by `TraderNpcs`. It differs in its origin
  (`TraderOrigin.BankerSpawnerRefId`), by which every client labels it (`TraderMarkers`), and in its clothes. No shop of
  `stocks.json` may be named `bank`: loading throws.
- **The key**: `TraderNpcs.FindNearestInFront` finds the nearest of the mode's people in front. `TraderShops` takes the
  key when that is a trader, `Bankers` when it is a banker.
- **Database**: `bank_accounts` (key `character_id`, `balance` never below 0). `BankBook` reads the balance, deposits
  and withdraws. A withdrawal of more than the account holds changes nothing.
- **No balance in memory**: `BankAccounts` reads through the reader and writes through the writer, one job at a time,
  so every move sees the balance the one before it left. Other features that pay into the bank or from it use
  `BankAccounts` too.
- **Gold is never in two places** (`Bankers`):
  - a deposit takes the gold from the purse first, then writes it. If the write fails, the gold goes back to the purse.
  - a withdrawal is written first, then the gold is given. If it cannot be given (the player left, or carries all they
    can), it is put back into the account.
  - if the server stops between the two halves, the gold is lost, never doubled.
- **The window** gets `BankState` (a `BankView`: balance and carried gold) as it opens and when either changes, and
  `BankAnswer` after each move. An answer that comes after the window closed shows as a notice in the corner.
- **Audit**: `economy.bank_deposit` and `economy.bank_withdraw`, with the amount and the balance after it.

## 5. Not built yet

- Paying from the bank elsewhere: rent, faction fees, job pay (https://trello.com/c/L9GG6BUv,
  https://trello.com/c/zKxxljeX, https://trello.com/c/chPqmTiy). A faction is still founded for carried gold.
- The city treasury (https://trello.com/c/25Uv1QC2) and a character's account going to it after a CK
  (https://trello.com/c/rqaX4LaA).
- The measured spot in the Imperial City (https://trello.com/c/S5kRwh65): the staff stand the banker by hand.
- A list of an account's movements. The audit log has them.
