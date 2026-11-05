# Playtest 7 Nov 2026: RpRoll dice and MESOL lore card

Branch `2026-11-06-playtest`, on top of `master` at `7b0d63e`. Two commits:

- `fac4ec4` Bring RpRoll's dice into the gamemode
- `103c9aa` Bring MESOL's lore card into the gamemode

Both features live in the core mod (`src/`, `ui/`, `locales/`), not as separate mods, so they use the gamemode's own
wheels, F10 menu and `/admin` panel. No other mod is needed. All tests pass (`./pack.ps1 -Test`: 338 Vitest,
499 xUnit, the packed mod on the real server 2/2). Tested in game by Pete with one player.

## Deploy on the test server

1. Stop the OblivionMP server (`pack.ps1` refuses while it runs).
2. Back up the database, so a rollback is one command:
   ```sh
   docker exec cyrodiilrp-postgres pg_dump -U cyrodiilrp -Fc cyrodiilrp > ~/cyrodiilrp-before-playtest.dump
   ```
3. Get the branch:
   ```sh
   git fetch origin
   git checkout 2026-11-06-playtest
   ```
4. **Install the UI packages again.** The dice board adds `three` (0.185.1) and `pack.ps1` runs `npm ci` only
   when `ui/node_modules` is missing:
   ```sh
   cd ui && npm ci && cd ..
   ```
5. Build and deploy: `pwsh ./pack.ps1` (or `pwsh ./pack.ps1 -Test`). It applies two new migrations,
   `20261105203008_Dice` and `20261105220717_Lore`, and keeps the deployed `server/config.json`.
6. Start the server. The log should say `Database at 20261105220717_Lore`.

**Config:** nothing to change. A new optional `lore` section has safe defaults
(`catalogMode: "Canon"`, `allowImplausibleSubtypes: false`).

**Permissions:** the Lore migration adds `alore` to the moderator and admin roles. Owners from `owners` get it
anyway.

### Rollback

```sh
git checkout master
docker exec -i cyrodiilrp-postgres pg_restore -U cyrodiilrp -d cyrodiilrp --clean --if-exists < ~/cyrodiilrp-before-playtest.dump
pwsh ./pack.ps1 -SkipDatabase
```

## What is in

### Dice (RpRoll)

`/roll` is replaced: d6, d10, d20 and d100 (d100 lands on 10..100), up to 5 dice, with a reason.

| Way in | What |
|---|---|
| `/roll [dice]` | `/roll`, `/roll 2d6`, `/roll 1d20+2d6`; a bare `/roll 20` is 1d20 |
| `/board`, **Home** | open or close the 3D dice board (mouse and keys) |
| `/dice [set]` | show or pick your dice set |
| **G** wheel | Dice: opens the board |
| **H** wheel (on a player) | Roll against: both players roll |
| **F10** | Dice tab: set, last rolls |
| `/admin` | Dice tab: the roll log of the server, filter by name |

### Lore card (MESOL)

A character card with 65 fields (origin, looks, traits, story...), each field Public or Private.
**Names are hidden:** a stranger sees only your race over your head and in chat (Imperial, Nord, Breton, Redguard,
Dunmer, Bosmer, Altmer, Argonian, Khajiit, Orc). They see your name after you introduce yourself, or always if you
set your name to Public. Private fields are seen by players you introduced yourself to.

| Way in | What |
|---|---|
| `/mylore`, **End** | your own card: edit with mouse or keys, roll a trait (dice land on the board) |
| `/lore [number]`, **Shift+End** | the card of the player in front of you |
| `/introduce [number]` | introduce yourself (the other player accepts or declines) |
| `/forget <number>` | that player no longer knows your name |
| `/name public\|private` | who sees your name |
| **G** wheel | Lore card |
| **H** wheel (on a player) | Introduce yourself, Read card |
| **F10** | Lore tab: name Public/Private, open the card |
| `/admin` | player card has a Lore section; `/alore <number> clear <field>` clears a field |

Texts are English by default, Polish is an extra (`locales/pl`).

## What to test with the team (needs 2+ players)

Already checked by Pete alone: the dice board, `/roll`, the F10 and `/admin` tabs, the own lore card, wheels G.
Not yet checked, because it needs a second player:

1. **Strangers:** two players who never met see each other's race, not the name, over the head and in chat.
2. **Introduce** through the H wheel and `/introduce`: the other player gets the ask, accepts, and then sees the name.
   Decline works too.
3. **Name Public** (F10 or `/name public`): everyone sees the name at once, without an introduction.
4. **Private fields:** a stranger does not see them on `/lore`, an introduced player does.
5. **`/forget`:** the name is hidden again for that player.
6. **Roll against** through the H wheel: both players roll, both see the result.
7. **Dice for others:** players around see your `/roll` result in chat (the board is your own).
8. **Admin:** `/admin` dice log shows rolls of all players; `/alore` clears a field of another player.

## Known gaps

- The ReadyM platform nickname (and the game's own character name) still carries the full name. Anything that
  shows it outside the gamemode's own nameplates and chat can still show the name to strangers.
- Friends and guilds as a third visibility level come later.
