# Notice board

## 1. What it is

A notice board stands in the world where the staff put it. Any player can read the notices pinned to it and pin one of
their own. A notice is unsigned: the board never says who wrote it, the database and the audit log do.

For now the board in the world is only a mesh, a blank wooden sign. Its notices show in a window over the screen, opened
with H in front of it. The server side is built so that a page drawn on the board itself would replace only the window.

## 2. Rules (as the player sees them)

- H in front of a board opens its window: the notices, newest first, each with how long ago it was pinned, a field to write
  one (Ctrl+Enter or Pin it) and Leave (Esc). The reader's own notices are marked Yours and have Take down.
- A notice holds 1 to 300 letters. Its lines keep their breaks, with at most one empty line between two, and without
  control characters or runs of spaces.
- A character has at most 3 notices up at once, on every board together, and pins at most one every 5 minutes. The window
  says which rule holds the player back and counts down to the next notice.
- A board holds 10 notices. A new notice on a full board takes the oldest one down.
- A notice stays up for 7 days, then comes off the board within a minute.
- The window closes when the player walks more than 5 m from the board, leaves the world, or the board is taken away.
- Every number above is in `noticeBoards` in `server/config.json` (`shown`, `minLength`, `maxLength`, `perCharacter`,
  `cooldownMinutes`, `expiryDays`), with `asset`, the game's form the board stands as. A value that makes no rule stops
  the mod at start with the reason in the log.

## 3. Ways in

| Way in | What it does | Code |
|---|---|---|
| `/aboard here` | Staff on duty: stands a board 1 m in front of them, facing them | `Notices/NoticeBoardCommands.cs` |
| `/aboard remove`, `list` | Staff: takes the board in front away with its notices, lists the boards | `Notices/NoticeBoardCommands.cs` |
| `/anotice list`, `remove <number>` | Staff: lists the notices up with who pinned them, takes one down | `Notices/NoticeBoardCommands.cs` |
| H in front of a board | Opens the board's window | `Notices/BoardWindows.cs` |
| The window | Pin a notice, take one's own down, Esc or Leave | `ui/notices`, `Notices/BoardOverlay.cs` (client) |
| The staff's panel, World tab | The notices up, with their author, board and date, and Take down | `ui/admin/WorldTab.vue`, `Admin/AdminPanel.cs` |

`/aboard` is an administrator's command, as `/anpc` is. `/anotice` is a moderator's too.

## 4. How it works

- **Database**: `notice_boards` (where a board stands: area, cell, position, yaw) and `notices` (board, character, text,
  `posted_at`, `expires_at`). A board taken away takes its notices with it, and so does a deleted character.
  `NoticeBoardBook` writes a new notice and the ones it takes down from a full board in one transaction. Expired rows are
  never read, and are deleted at the next start.
- **In memory**: `NoticeBoards` keeps the boards and the notices up (`NoticeBoardRegistry`, which also holds the rules).
  Every change is written first and kept after, so a restart reads back what players saw. While a notice is being
  written, its board and its author take no second one.
- **The board in the world**: each player's game stands the boards of the area its character is in as a mesh of its own
  (`Notices/BoardProps.cs`, server and client). The server tells a player the boards as they come into an area, as a
  board there is stood or taken away, and when their game asks after a load.
- **What players get**: a board's notices go only to the windows open on it, when they change (`BoardState`, a
  `BoardView`). A player's own count and wait go to their window when their notices change on any board. The
  board's spots go only to the players in its area.
- **Unsigned**: the view carries no author, only whether a notice is the reader's own. The staff see the author's
  character in `/anotice list` and in the panel.
- **Audit**: `board.place`, `board.remove`, `notice.post` (with the notices a full board took down), `notice.take_down`
  (by its author) and `notice.remove` (by the staff), each with the notice's text.

## 5. Not built yet

- The notices drawn on the board itself. The platform puts a page on the screen only.
- A check in the game of how the board looks: the mesh, its height and the side it faces.
