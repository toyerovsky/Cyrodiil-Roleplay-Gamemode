# Cyrodiil RP: context and instructions for agents

The instructions apply to the whole repository. Before a change, read the description below and the docs for the task.

## What the project is

**Cyrodiil RP is a roleplay mode for OblivionMP**, multiplayer Oblivion on the ReadyM platform. A player makes a
character of their own, a resident of Cyrodiil, and plays it among other players. The mode is a server mod with a
client half (`client/`) and a server half (`server/`), built on the platform's SDK 1.0.

- **A character instead of an account:** the mode's form gives the first name and surname, the game's creator the
  look. The name is unique on the server.
- **Welcome screen:** every visit starts with it: the account's character (last played, time played) and Play, an
  empty slot with New character, the second slot locked until the platform keeps a game save per character. For now
  one character per account, so that nobody gains anything from the save.
- **RP chat:** speech in range, `/me` (also over the character's head), `/do`, shout, whisper, rolls, local and
  global OOC, private messages. In-character lines name the speaker as the reader knows them (see the lore card).
- **Names over heads:** drawn by the mode: the character's name to those who know it, the race to strangers (see the
  lore card), `[Staff]` in another colour on duty.
- **Interaction wheels:** G opens the actions of your own character, H the actions towards the player in front of
  you.
- **Party:** an invitation from the H wheel, taken up with Y from the card at the top of the screen (N declines), a
  leader, kicking, leaving from the G wheel, the `/p` chat, a panel of the members on screen with their health and
  distance.
- **Factions:** lasting organisations of characters (guilds, gangs, houses, companies), kept in the database. Founded
  with `/faction create` or in the F10 menu for a fee from carried gold, one a character. Ranks from the top down,
  made and named by the faction, each with permissions (invite, remove, manage ranks). An invitation from the H wheel
  or `/faction invite`, taken up with Y, in the menu or with `/faction accept`. `/f` is the faction's chat. See
  `docs/features/factions.md`.
- **Trader:** an NPC placed with the staff command `/anpc here trader <shop>`, without the game's services (the
  game's trading and rooms do not apply to it). H over it opens a shop window with the game's icons: food, drinks,
  clothes, potions, tools, weapons, armour, books. It only sells. It cannot be killed for good.
- **Bank:** a banker NPC, stood by the staff as a trader is (`/anpc here banker`) and labelled Banker. H over it
  opens the bank's window: the character's gold at the bank and the gold carried, a deposit and a withdrawal. The
  balance is the character's, kept in the database over deaths and restarts. See `docs/features/bank.md`.
- **Spawn:** the staff set it in the outside world (`/aspawn here`, a button in the panel), a new character goes
  there after closing the game's creator.
- **Exchange:** an offer from the H wheel, taken up with Y as for a party, gold and items from hand to hand in a
  window, both sides see both offers and confirm. Names come from the catalogue the mode ships, so what a player wears
  and what players made (their potions and enchanted items) stay out of it.
- **Dice:** `/roll` rolls d6, d10, d20 and d100 (up to 5 dice) on the server and shows the line to those around. A 3D
  board (Home, `/board`, the G wheel, the F10 menu) throws them with the mouse or the keys and lands each die on the
  server's number, in one of 12 dice sets kept on the character (`/dice`). The H wheel adds an opposed d20 roll. Every
  roll is logged.
- **Lore card:** a character's card (End, `/mylore`, the G wheel, the F10 menu), its fields picked from a lore list or
  written by the player, each Public or Private. Traits are typed once or rolled with 5d20. Shift+End, `/lore` and the
  H wheel read another player's card. Strangers see a character's race instead of the name, over the head, in
  in-character chat and in notices. They see the name after an introduction (a Y/N ask from the H wheel or
  `/introduce`, taken back with `/forget`), or always if the owner makes it public (`/name`).
- **Player menu (F10):** tabs with the game's medallions: character (name, since when, time played, gold), account
  (ReadyM nick, previous visit, sanctions in force, the language choice when the server knows a second one), dice
  (the set, the last rolls), lore (name Public or Private, the card as the public reads it), faction (found one, answer
  an invitation, or the roll and the ranks with their buttons, `/faction` opens this tab), help with the player's
  commands under headings (`/help` opens this tab) and the mode's keys.
- **In-game administration:** staff roles from the database (moderator, administrator, owner), duty with `/duty`
  (staff actions only on duty, which shows in the name over the head), sanctions with a reason and an audit log
  (kick, ban, mute, freeze, warning, lifting them also for offline players), care of a character (healing, health,
  reviving, gold, the game's items by name and kind, for oneself and for players), a message to a player, teleports
  (to a player, to the gates of cities and settlements, back, and `/astep`, a few metres ahead through a wall or
  straight up or down, as the platform has no noclip), the area's time and weather. The `/admin` panel (also from
  the G and H wheels for the staff): players with a card and their lore card (`/alore` clears a field), items,
  sanctions in force, the audit log by pages, the world, the roll log, the factions with their ranks and
  members (`/afaction` dissolves or renames one, makes a member a leader or removes one).
- **Language:** English (`locales/en`) and Polish (`locales/pl`), chosen in the F10 menu, saved on the account. The
  game's item names are in English in both, because the Polish ones are in the remaster's `Game.locres`, readable
  only with Oodle.

## Where to find information

| Topic | Source |
|---|---|
| Architecture, running, tests, deployment | [DEVELOPMENT.md](DEVELOPMENT.md) |
| The OblivionMP platform and SDK | [.claude/skills/oblivionmp-platform/SKILL.md](.claude/skills/oblivionmp-platform/SKILL.md), the docs at https://docs.ready.mp/oblivion-mp/docs/what-is-oblivion-mp |

Infer behaviour from the code and the tests. A description in the docs does not confirm that a feature works on the
server.

## Working on the repository

The mode in **C#** (`src/`), the pages in **Vue 3 / Vite** (`ui/`), **PostgreSQL / EF Core** with a migrator of its
own. The commands are in `DEVELOPMENT.md`. Before working in an area, read:

- Code and code review: the coding standard, [CODING-STANDARD.md](CODING-STANDARD.md), with this repo's rules for
  C#, the update thread and what is sent to players. Claude Code loads it on its own in `src/` and `tests/`.
- Tests and verifying a change: [.claude/skills/testing/SKILL.md](.claude/skills/testing/SKILL.md).
- Texts for players and pages: [.claude/skills/game-ui/SKILL.md](.claude/skills/game-ui/SKILL.md).
- Previewing pages without the game and Playwright: [.claude/skills/ui-playwright/SKILL.md](.claude/skills/ui-playwright/SKILL.md).
- Integration with the platform: [.claude/skills/oblivionmp-platform/SKILL.md](.claude/skills/oblivionmp-platform/SKILL.md).

### Mandatory verification of every new interface

1. An E2E test in headless Playwright through a real player's flow, run once at the end of the work on a page. In
   between, Vitest tests are enough. A build, a render test or a unit test alone does not replace E2E.
2. Screenshots of the states that matter at 1366x768, 1920x1080, 1280x1024 and 2560x1080 (`E2E_SIZES=all`), **opened
   and judged by eye**: readability, wrapping, nothing cut off, scrolling, visible focus.
3. Fixes, verification again, and in the summary a list of the scenarios and resolutions checked and of what cannot
   be checked without the game.

Screenshots, traces and reports stay in `ui/test-results/` and `ui/playwright-report/` (in `.gitignore`).

Do not commit or deploy unless the user asks. `./pack.ps1` builds the mod and installs it on the local server,
`-PackOnly` only builds it.

Files end lines with LF. A script on Windows keeps them so (Python: `read_bytes` and `write_bytes`, not text mode).
Never an em-dash, anywhere (code, texts, docs, commit messages).
