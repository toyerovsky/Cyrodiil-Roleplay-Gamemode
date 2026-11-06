# Lore card and hidden names (MESOL in the core)

Branch `2026-11-06-playtest`, commit `103c9aa` "Bring MESOL's lore card into the gamemode", on top of the dice commit
`fac4ec4` (master `7b0d63e`).

## 1. What it is

A character card of 65 fields (identity, heritage, life, looks, story, status, traits) picked from an 859-entry Elder
Scrolls lore list or typed, each field Public or Private. On top of it, names are hidden: other players see a
character's race until the two introduce themselves or the owner makes the name public. This applies to the nameplates,
the in-character chat and every player-facing notice.

Origin: MESOL 0.4.2 was Pete's standalone OB:MP mod. It is ported into the core so it can use the gamemode's own wheels,
F10 menu, `/admin` panel, Asks card, chat and dice, which a separate mod cannot reach. Friends, friendship offers and
blocking from MESOL are dropped for now.

## 2. Rules (Pete's, as the player sees them)

- A stranger sees only your race, as the English lore name, the same for both sexes and never translated: Imperial,
  Nord, Breton, Redguard, Dunmer, Bosmer, Altmer, Argonian, Khajiit, Orc. A race the table does not know shows
  `Stranger`.
- They see your name once you introduce yourself to them (they accept), or always, if you set your name to Public.
- Every card field is Public (everyone) or Private (only players you introduced yourself to). `notes` is owner-only and
  locked: nobody but you and the staff reads it.
- An introduction is a handshake: one offers, the other accepts with Y or declines with N within 30 s. An accepted
  introduction works both ways. After an offer the asker waits 10 s before the next one.
- `/forget <number>`: that player no longer knows your name (one direction only).
- Friends and guilds, as a third visibility level, come later.
- The staff panel always shows real names. OOC lines (`/b`, `/ooc`, `/pm`) keep the OOC and account names.

Card editing (from MESOL): catalog fields pick from the lore list (era, race scope, native lands first for homeland);
`subtype` and `culture` need `race`, `homeRegion` needs `homeland`, `rank` needs `affiliation`. A trait is typed once and
fixed, or rolled 5d20 (5..100) with one reroll and then fixed. A card holds at most 16,000 characters of text.

## 3. Ways in

| Way in | What it does | Code |
|---|---|---|
| `/mylore`, End | Open or close your own card (editor) | `Lore/LoreCommands.cs`, `Client/Lore/LoreOverlay.cs` |
| `/lore [number]`, Shift+End | Read the card of the player in front, or by number | `LoreCommands.Read`, `LoreWindows.OpenInFront` |
| `/introduce [number]` | Offer an introduction to the player in front or by number (`Speaks`) | `Lore/LoreIntroductions.cs` |
| `/forget <number>` | That player no longer knows your name | `LoreIntroductions.cs` |
| `/name public\|private` | Who sees your name | `LoreCommands.ChooseName` |
| Asks card (top of screen) | Y accepts, N declines an introduction (`AskKinds.Introduction = "introduce"`) | `Players/Asks.cs`, `ui/asks/kinds.js` |
| G wheel | "Lore card" (`self.lore`, Order 25, icon `book`): opens the editor | `LoreCommands.cs` |
| H wheel | "Read card" (`player.lore`, Order 11, `book`); "Introduce yourself" (`player.introduce`, Order 12, `hand`, `Speaks`, reach = say range, hidden once both know each other, shown closed during the 10 s wait) | `LoreCommands.cs`, `LoreIntroductions.cs` |
| Card, mouse | Click everything: sections, fields, picker, visibility, roll, Nearby list and its buttons | `ui/lore/App.vue` |
| Card, keys | Up/Down field, Left/Right or Q/E section, PgUp/PgDn scroll, Enter edit, Space visibility, Delete/Backspace clear, R roll a trait, P public preview, Esc close. In a text box: Enter saves (Ctrl+Enter in a long text), Esc cancels. On Nearby: Enter read, `=` introduce or accept, `-` decline, Delete forget | `ui/lore/App.vue` |
| Card, Nearby | Players within 20 m: header, distance, relation, pending offers both ways | `LoreDesk.BuildNearby` |
| F10 | Tab `lore`: name Public/Private switch, "open the card" (closes the menu), how strangers see you, your card as the public sees it, online players who know you. Keys tab lists End and Shift+End | `ui/menu/LoreTab.vue` |
| `/admin` | Player card: "Lore card" section, read on request, every field at staff level under the real name, a clear button per field (asks twice) | `ui/admin/LoreSection.vue`, `PlayerCard.vue` |
| `/alore <number> clear <field>` | Staff: empty one field, audited (`admin.lore_clear`), the owner is told | `LoreCommands.ClearForStaff` |

## 4. How it works

### The card

The page `lore` asks, the server answers. One request at a time, by request id, one JSON answer under the same id:

1. End / G / F10 / `/mylore` -> `LoreWindows.OpenEditor` -> `LoreAnswer(0, {"op":"edit"})`. Request id 0 is the server's
   push and opens the window. Shift+End / H "Read card" / `/lore` -> `LoreAnswer(0, {"op":"view", ...})`.
2. The page sends `LoreAsk(id, json)` with an `op`: `load`, `options`, `set`, `pick`, `vis`, `clear`, `roll`, `keep`,
   `preview`, `namevis`, `nearby`, `view`, `introduce`, `accept`, `decline`, `forget`.
3. `LoreDesk.Handle` rate-limits (40 requests per 10 s), routes, and answers with `LoreAnswer(id, json)`. Field edits go
   through `LoreEditor` (MESOL's `CardEditor` port) and `LoreCards.Save`, which writes the changed rows through
   `Persistence.Writer`.
4. A trait roll goes through `Rolls.Roll(player, 5 x d20, field label)`, so those around read "rolls 5d20 for <trait>",
   then `DiceBoards.Present` lands the dice on the small board above the card.

The exact JSON shapes (Field, Value, View, Option, Relation, Header) are in the "Final contract" of `handoff/PLAN-mesol.md`.

### Server-authoritative parts

- **Visibility.** Everything another player gets of a card goes through `LoreCuts`: Owner and Staff see all,
  Acquaintance all but owner-only, Stranger only Public. The page never holds a field its reader may not see.
- **Who is an acquaintance:** `LoreCardViews.FindViewer`: the reader knows the subject (`Introductions.Knows`).
- **Names.** `ShownNames.Shown(reader, subject)`: self, a known name or a public name gives the full name; otherwise the
  race label from `CharacterRaces` (the game's `MainCharacter.Race` form id, or the race asset path as a fallback). A
  player without a character keeps their number or nickname.
- **Chat.** Texts carry a `ShownName(OnlinePlayer)` argument. `RpChat.Translate` resolves it per reader before the
  `Localizer`, and `TranslateOncePerLocale` skips its per-locale cache for such texts. Converted: say, `/me`, `/do`, `/s`,
  `/w`, `/try`, greet, `/roll`, roll against, the trait roll, the introduction line, `/players`, party and trade notices,
  the asks card, the party panel and the trade window, and the H wheel's header.
- **Nameplates.** `ShownNames` checks every second and sends each reader `KnownNames(json)`, `{"<number>": "<name>"}`,
  when their map changes (join, leave, introduction, forget, name public/private, duty mark, race change). `[Staff] `
  is kept in front for staff on duty. Client `Nameplates` draws only names in the map: a player not in it shows nothing.

### RPCs (`src/CyrodiilRP.Common/RpContracts.cs`)

| Direction | Message |
|---|---|
| C->S | `OpenLore(bool look)` (End false, Shift+End true), `LoreAsk(int requestId, string json)`, `CloseLore()`, `ChooseNamePublic(bool namePublic)` (F10 switch) |
| S->C | `LoreAnswer(int requestId, string json)`, `CloseLoreCard()`, `KnownNames(string json)` |

CEF page `lore`: the game sends `Locale`, `Answer(requestId, json)`, `Closed`; the page calls `sendLoreReady`,
`sendLoreAsk`, `sendLoreClose`, `sendLoreTyping(bool)`. The F10 page calls `sendMenuLore` and `sendMenuLoreName(bool)`.
`/admin`: `AdminAsk("lore", "<number>")` -> `AdminPart("lore", AdminLoreView)`; clear runs
`AdminRun("/alore <number> clear <fieldId>")`.

## 5. Implementation map

| Area | Path | What |
|---|---|---|
| Server | `src/CyrodiilRP.Server/Lore/LoreDesk.cs` | The page's ops, rate limit, trait roll, nearby |
| Server | `src/CyrodiilRP.Server/Lore/LoreWindows.cs` | Open windows, pushes, request size cap, answer size guard (`Fit`) |
| Server | `src/CyrodiilRP.Server/Lore/LoreCards.cs`, `LoreCardRegistry.cs` | Card cache per online character, load on join, saves |
| Server | `src/CyrodiilRP.Server/Lore/LoreEditor.cs`, `LoreFields.cs`, `LoreCatalog.cs`, `mesol-lore.json` | Editor rules, the 65 fields, the embedded catalog (859 entries) |
| Server | `src/CyrodiilRP.Server/Lore/LoreCuts.cs`, `LoreViews.cs`, `LoreCardViews.cs`, `LoreCardText.cs` | Visibility cut, view and header building |
| Server | `src/CyrodiilRP.Server/Lore/ShownNames.cs`, `Players/ShownName.cs`, `GameRaces.cs`, `CharacterRaces.cs` | Shown names, per-reader resolution, race table |
| Server | `src/CyrodiilRP.Server/Lore/Introductions.cs`, `IntroductionRegistry.cs`, `LoreIntroductions.cs` | Who knows whom (cache + DB), the handshake, `/introduce`, `/forget`, H option |
| Server | `src/CyrodiilRP.Server/Lore/LoreCommands.cs`, `LoreTargets.cs` | `/mylore`, `/lore`, `/name`, `/alore`, G and H read options, "in front" and Nearby |
| Server | `src/CyrodiilRP.Server/Admin/AdminLoreView.cs`, `AdminParts.Lore`; `PlayerMenu/MenuLoreView.cs`, `MenuKnownView.cs` | `/admin` and F10 views |
| Server | `Chat/RpChat.cs`, `Chat/ChatCommands.cs`, `Interactions/InteractionMenu.cs`, `Party/*`, `Exchange/Exchanges.cs`, `Players/Asks.cs` | Switched from `x.Name` to `ShownName` |
| Client | `src/CyrodiilRP.Client/Lore/LoreOverlay.cs` | `lore` page window, End / Shift+End binds, typing flag |
| Client | `src/CyrodiilRP.Client/Nameplates/Nameplates.cs` | Draws the `KnownNames` map instead of reading nicknames |
| Data | `src/CyrodiilRP.Data/Entities/LoreField.cs`, `LoreIntroduction.cs`, `LoreVisibility.cs`, `Lore/LoreBook.cs` | Entities and static data access |
| UI | `ui/lore/` | `App.vue`, `FieldList.vue`, `FieldDetail.vue`, `LorePicker.vue`, `TextEdit.vue`, `NearbyList.vue`, `NearbyDetail.vue`, `CardView.vue`, `asker.js`, `card.js`, `bridge.js` |
| UI | `ui/menu/LoreTab.vue`, `ui/admin/LoreSection.vue`, `ui/asks/kinds.js`, `ui/rp/icons.js` (`book`) | F10, `/admin`, asks, wheel icon |
| Art | `ui/rp/art/icons/lore.webp`, `ui/rp/art/lore-book.webp` | Pete's LORE book medallion |

**Database** (migration `20261105220717_Lore`):

- `lore_fields`: PK (`character_id`, `field_id` varchar(32)), `catalog_id` varchar(64) null, `text` varchar(2000) null,
  `number` int null (check 5..100), `fixed` bool, `rolls` varchar(128) null (`"5d20:3,17,9,11,6=46;..."`), `visibility`
  varchar(32) (`Public` | `Private`), `updated_at`. FK to `characters`, cascade.
- `lore_introductions`: PK (`knower_character_id`, `known_character_id`), `at`; check knower <> known; both FKs cascade;
  index on `known_character_id`. One row per direction.
- `characters.name_public` bool, default false.
- The migration also adds `alore` to the `admin` and `moderator` rows of `staff_roles`.

**Config** (`lore` section, optional, not in the shipped `config.json`): `catalogMode` `Canon` (default) | `CanonPlus` |
`Free`; `allowImplausibleSubtypes` false (true also offers kinds that do not fit the race's one body, such as the Senche).

**Permissions:** `StaffPermissions.Lore = "alore"` for `/alore` and the clear buttons, granted to moderator and admin;
owners hold every permission. Reading a card in `/admin` needs only the panel permission.

**Locales:** `locales/{en,pl}/lore.json` (generated `LoreText`), `locales/{en,pl}/ui.lore.json`, plus `ui.admin.json`,
`ui.asks.json`, `ui.menu.json`. Catalog names and summaries stay English only.

**CEF page and layer:** `lore`, `WebPages.LoreLayer`: 4 on the playtest branch (dice moved to 5 above it), 5 on the
Sanctuary branch.

**New npm dependencies:** none.

## 6. Design decisions and why

- **Names resolved per reader, at send time.** One `ShownName` argument in the phrase, resolved in `RpChat.Translate`,
  so every converted line is right for each listener without changing each call site's logic.
- **A pushed map for nameplates (`KnownNames`), not nicknames.** The platform nickname carries the full name, so the
  client no longer reads it. Unknown players get no plate rather than a leaked one.
- **Checked every second.** A race change in the game's creator reaches the server without an event.
- **Private until the card loads.** `IsNamePublic` is false while the card is loading, so a name never shows early.
- **Introductions through the core's Asks.** Same Y/N card, 30 s timeout and 10 s cooldown as party and trade invites.
- **Visibility cut on the server only** (`LoreCuts`), so a modified page cannot read hidden fields.
- **`shown` is null for typed text fields.** A full Polish card with the words twice would pass one message's limit
  (a string carries a 2-byte length, 65,534 bytes of UTF-8). `LoreWindows.Fit` refuses any answer over the limit instead of
  throwing, and `LoreAnswerSizeTests` covers it.
- **End vs Shift+End.** Shift+End also fires End's bind, so End waits 0.05 s to see whether it was a look.
- **`sendLoreTyping`.** While a text box has the keyboard, End moves the caret instead of closing the card and losing the
  words.
- **Lore layer under the dice layer**, so a trait roll lands on the small board over the card.
- **Two visibility levels only.** MESOL's Known, Friends and Private defaults map to Private, except `notes`.

## 7. Tested / not tested

Automated (counted from the commit):

- xUnit: 87 cases (59 facts, 5 theories with 28 rows) in `tests/CyrodiilRP.Tests/Lore/` (`LoreEditorTests` 21,
  `LoreCutsTests`, `LoreFieldsTests`, `ShownNameTests`, `LoreCatalogTests`, `GameRacesTests`,
  `IntroductionRegistryTests`, `LoreRollFormatTests`, `LoreAnswerSizeTests`) and `Database/LoreBookTests.cs` (7, real
  PostgreSQL). `Players/AskBookTests.cs` updated.
- Vitest: 32 hand-written (`ui/tests/lore.test.js` 27, `admin.test.js` 2, `menu.test.js` 2, `asks.test.js` 1), plus the
  page contract tests generated from `ui/pages.js`.
- Playwright: 12 (`ui/e2e/lore.spec.js` 10, `admin.spec.js` 1, `menu.spec.js` 1), at 4 sizes.
- Suite on the playtest branch: 338 Vitest, 499 xUnit, real server 2/2.

In game: Pete tested alone and it was fine: his own card (edit with mouse and keys, trait roll on the board), the F10
and `/admin` tabs, the G wheel.

Not tested (needs 2 players), from `PLAYTEST.md`:

1. Two strangers see each other's race, not the name, over the head and in chat.
2. Introduce via the H wheel and `/introduce`: accept, then the name shows; decline.
3. Name Public (F10 or `/name public`): everyone sees the name at once.
4. Private fields: hidden on `/lore` for a stranger, shown for an introduced player.
5. `/forget`: the name is hidden again for that player.
6. `/alore` clearing another player's field.

## 8. Known gaps and next steps

- **The ReadyM platform nickname still carries the full name**, and so does the game's own character name
  (`Nicknames` writes both). Anything that reads those outside the gamemode's nameplates and chat can still show the
  name to strangers.
- **OOC name fallback.** `OnlinePlayer.OocName` falls back to `Name` (the full character name) when no account name was
  captured. `/players`, `/b`, `/ooc` and `/pm` use it, so in that case the name shows next to the race.
- **No range check by number.** `/introduce <number>`, `/lore <number>` and the card's `view`/`introduce` ops take any
  online player by number. Only the H wheel's "Introduce yourself" is limited to the say range.
- **Friends and guilds** as a third visibility level come later.
- **Optional `AdminTabs.Lore`** (a tab listing online players' cards) was not built.
- **Wheel caps:** see `dice.md`. The lore card adds one G option and two H options to wheels capped at 8
  (`WheelOptions.MaxOptions`): staff lose "Staff panel" on G and staff options on H. Needs Przemysław's decision.
- **Page bundle:** 1013 KB of the 1024 KB budget on the Sanctuary branch (all three features).
