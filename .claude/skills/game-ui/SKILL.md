---
name: game-ui
description: How Cyrodiil RP shows anything to a player. Use before writing player-facing output (chat lines, notices, refusals) or any page in ui/, and when reviewing one.
---

# Game UI

## Where things go

| What | Where | How |
|---|---|---|
| Speech, emotes, OOC | the chat page, `ui/chat` | `RpChat.SendAround`, `ChatLineKind` picks the colour |
| A command's answer or refusal | the chat, to the sender alone | `call.Reply`, `call.Refuse` |
| Something that just happened to a player | a notice in the corner | `RpChat.Notify` (the game's own message) |
| What greets every join: the characters, Play, New character | the welcome, `ui/welcome`, opaque over the game, holding the input | `Welcomes` sends `Welcome` |
| A form (the character's name) | a page in `ui/<page>` over its own viewport | `NameFormOverlay` |
| An action on oneself (G) or on the player in front (H) | the wheel, `ui/radial` | a `SelfOption` or an `InteractionOption` added to `SelfMenu` or `InteractionMenu` |
| An action that needs more from the player (a reason, a message) | the chat, the command started for them | `RpChat.Prefill(player, "/pm 3 ")` |
| Gold and items from one player to another | the trade window, `ui/exchange`, both sides confirm | `Exchanges`, from the wheel's Trade |
| Who is in the player's party | the panel at the top left, `ui/party`, only to be read | `Parties` sends `PartyState` |
| Buying from a trader | the trader's window, `ui/trader`, the game's icons and names | `TraderShops`, H over the trader |
| The player's own record, the commands, the keys | the player's menu, `ui/menu`, F10 or `/help` | `PlayerMenus` sends `MenuState` |
| Throwing dice | the dice board, `ui/dice`, three.js, the server picks every number | `DiceBoards` sends `DiceState`, `Rolls` is the one place that rolls |
| A character's card, one's own or another's | the lore card, `ui/lore`, one request and one answer by id | `LoreWindows` opens it, `LoreDesk` answers `LoreAsk` |
| The player's faction: founding, the roll, the ranks | the menu's faction tab, `ui/menu/FactionTab.vue`, every button a `/faction` command | `FactionMenu` runs it and answers `FactionAnswer` with the menu |
| The staff's tools | the staff's panel, `ui/admin`, `/admin` or the wheels, no key | `AdminPanel`: every button a command of `AdminActions` |
| A staff action that needs more (a reason, minutes) from the panel | a dialog over the panel that checks it as the server would | `ui/admin/ActionDialog.vue` |
| Held by the staff (frozen) | a plate at the top, `ui/frozen`, holding the input | `Freezes` sends `Frozen` |

- No chat line as a fallback when a page does not show. A page that does not load is a bug to fix, not a second path.
- Every line is written in its reader's language (`OnlinePlayer.Locale`).
- A character's name in a text for players is a `ShownName`, resolved per reader in `RpChat.Translate`: the name to
  those who know it, the race to strangers. Never `player.Name`.

## The wheels

- An option is a wedge: `Label` (a generated phrase), `Icon` (a `WheelIcons` name, drawn by `ui/rp/icons.js`), `Order`
  (players' options first, the staff's from 100), `Permission` (a `StaffPermissions` name, hides it from others),
  `Speaks` (said in character or out, so a mute hides it). Its `State` is one function giving `OptionState.Hidden`,
  `OptionState.Close(reason)` (shown disabled with the reason) or `OptionState.Open`. The pick is checked again before
  `Run`.
- An option that does what a command does runs the command (`commands.Run(player, "/roll", chat)`), so its checks and
  refusals are the command's own. One that needs typing starts the command in the chat.
- Eight places a wheel. The server picks the player in front from the positions it holds (`Proximity.FindNearestInFront`).

## Pages

- Vue 3 composition API (`<script setup>`), plain JavaScript. Names and comments as in `CODING-STANDARD.md` (`names`,
  `comments`).
- A page talks to the game only through its `bridge.js` (`readym.on("X")`, `readym.sendX(...)`). On the C# side the
  `[Service]` that owns the page declares its messages itself and `Mod.Init` registers it as `ICefRpcHandler`.
- The pages ship with the mod to every player: `check-dist` keeps them under its budget, and only the Latin font subsets
  go in.
- Components from `ui/rp`: `tokens.css` (palette, type, sizes), `base.css` (scrollbars, selection, reduced motion),
  `components.css` and a Vue file per component (`RpPanel`, `RpField`, `RpButton`, `RpChip`, `RpTabs`). A piece a page
  would build by hand becomes a component there, shown in the gallery (`ui/dev/Gallery.vue`).
- A choice among a few (a kind of wares, a filter, a language) is a row of `RpChip`, toggle buttons with `pressed`
  (`aria-pressed`): each one a stop of Tab, picked with Enter or Space. Not radios, whose arrows the pages' tabs take.
  A chip without `pressed` acts at once (a weather).
- Tabs are `RpTabs`: the game's medallions down the side, `size` large (the player's menu) or small (the staff's panel).
- A page drawn over the game imports `ui/shared/click-through.css`, so its html and body let the clicks through, and
  sets `pointer-events: auto` on what takes them.
- No style uses the `font` shorthand (`font: inherit` aside): it resets the full-height figures `base.css` gives the body,
  and Alegreya's own 0 reads as an o. A test keeps to it. A number carries its unit: "40 gold".
- A page that asks something of the player before they play (the name) is a letter: `RpPanel variant="letter"` on the
  game's parchment and frame, `RpIlluminated` for its first paragraph. Pictures live in `ui/rp/art` (`DEVELOPMENT.md`,
  Pages). A picture that is only decoration has `alt=""`.
- Sizes are px in source for 1080p. The build turns them into rem (`postcss-pxtorem`) and `ui/shared/scale.css` scales
  the root with the window. Never write rem or vw by hand.
- Texts from `locales/<language>/ui.<page>.json`. A limit a text names comes from the server's constant
  (`ui/shared/limits.js`, tied to the C# by a test), filled into a placeholder.
- A new page is listed in `ui/pages.js` with its scenarios, gets a `bridge.js`, a `pretend.js` and an entry in
  `tests/contract.test.js`. Its owner in the client holds it through `OverlayPage`, a window the server's state opens
  through `PageWindow`.

## Reading it at a glance

At 1366x768 everything is 0.71 of its 1080p size.

- No text under 14 px. Contrast 4.5:1 for every text the player reads.
- Dim with a colour, never with `opacity`: a disabled button has its own colours. Text on gold is ink, not white.
- Text over the game sits on a plate dark enough for 4.5:1 over a sunlit scene (`--night`).
- Text over the game gets a dark plate (the chat's log) or a strong shadow.
- A refusal marks the field it is about (`aria-invalid`) and says why (`role="alert"`).

## Keys

- A held key is one press (`ui/shared/press.js`). Typing and the caret in a field still repeat. A held Enter would send
  again what the server just refused.
- Keys an input method is still composing are not the page's (`isComposing`).
- Esc gives the game back its keyboard. Only a page holding the keyboard can give it back. A form the server requires
  (the character's name) has no Esc: it stays until it is answered, and so does the welcome until the player chooses and
  the frozen notice until the staff lift it. A dialog over a page closes on Esc before the page does.
- A page that loses the keyboard to another (the chat when the name form opens) says so without handing it to the game,
  or it would take it from the form.
- Tab in a page with one field is kept, or it would only lose the keyboard.
- The mode's keys: G own wheel, H the one in front, K chat, F10 the player's menu, Home the dice board, End the lore
  card, Shift+End the card of the player in front. A new bind goes into the menu's Keys
  tab (`ui/menu/tabs.js`), a test ties the two. The key that opens a window closes it too, through its bind only: the
  page leaves it alone, or it would close there and the bind would open it again.
- Tabs switch on Q and E, read by the key's place, and on the arrows. In game `event.code` is empty, so a page reads
  places through `readCode` (`ui/shared/press.js`). `RpTabs` takes them while a tab has the focus. A page without fields
  turns its tabs from anywhere by handing its keys to `takeKey` (the menu), a page with fields leaves Q and E to them
  (the staff's panel).

## Motion

Short and quiet. Every transition goes through the tokens' durations, and `base.css` turns them off under
`prefers-reduced-motion`.

See also the `ui-playwright` and `testing` skills.
