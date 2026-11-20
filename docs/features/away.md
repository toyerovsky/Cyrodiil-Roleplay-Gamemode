# Away from the keyboard

## 1. What it is

`[AFK]` in grey before the name over the head of a player who has done nothing for a few minutes, so those near know a
silent character is not refusing to talk, and a screen over the player's own game that asks first and then says so.

## 2. Rules (as the player sees them)

- **Signs of a player at the keyboard**: a step, a turn of the camera, the chat opened to write, and a key or the mouse
  in one of the mode's windows (reading the bank's window is being there). Standing still is not a sign by itself.
- **The question.** 30 s before the mark (`AwayRules.WarnFor`, at most half of `away.afterMinutes`) the screen asks
  "Still there?" and counts down to the mark. Nothing shows over the head yet. Any sign ends the question.
- **The mark.** After `away.afterMinutes` (5 by default) with no sign, `[AFK]` shows over the head and the screen says
  "You are AFK" with the time since the last sign, counting on.
- **Back.** The first sign takes the mark and the screen away at once, without a click, with a notice in the corner.
- `/afk` (`/away`) marks the player at once. A sign in the first 2 s after it (`AwayRules.ManualGrace`) does not count:
  the hand leaving the keys is no return.
- Nothing else changes: an AFK character is still a character. No kick, no teleport.

## 3. Ways in

| Way in | What it does | Code |
|---|---|---|
| A few still minutes | The question, then the mark | `Players/AwayWatch.cs` |
| `/afk`, `/away` | The mark at once | `Players/AwayWatch.cs` |
| The screen | Only shows, takes no keys or clicks | `ui/away`, `Away/AwayOverlay.cs` (client) |

## 4. How it works

- **The server** (`AwayWatch`) looks at every player in play once a second: the position and the camera's turn from
  the entity, with the network's jitter left out (`AwayRules.MoveSlack`, `TurnSlack`). The chat reports writing
  (`ReportTyping`), the client reports a key or the mouse in a page (`ReportPresence`). `AwayRules.StageAfter` turns
  the time since the last sign into a stage: here, warned, away.
- **The client** passes a page's touch on (`PresenceReports`): every page reports one at most every 5 s
  (`ui/shared/presence.js`, from `ui/shared/start.js`), and the client sends it to the server at most every 30 s, or
  every second while the screen is up, so the screen goes at the first touch.
- **The screen** gets `AwayWarning` (seconds left), `AwayMarked` (seconds since the last sign) and `AwayEnded`. The page
  counts the seconds on by itself. Its layer is over the chat's log and under every window (`WebPages.AwayLayer`), so a
  window the player opened covers it. The client shows it again after each load, as the game rebuilds its overlay.
- The mark is the name's prefix (`Nameplate.AwayMark`), drawn grey by `Nameplates`.
