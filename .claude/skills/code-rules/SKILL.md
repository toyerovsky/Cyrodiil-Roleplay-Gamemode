---
name: code-rules
description: The coding rules of Cyrodiil RP for C# and Vue. Use before writing or reviewing any code in this repo.
---

# Code rules

Simple, idiomatic code that a reader understands without the author. The user reads English as a second language.

The C# coding standard, [`CODING-STANDARD.md`](../../../CODING-STANDARD.md), applies in full. What follows is this
repo's own on top of it.

## Names

- Full English words: `capabilities`, not `caps`. Accepted short names: `t` (texts), `ct`, `db`, `pg` (the tests'
  database fixture), and one-line lambda parameters (`x => x.Id`).
- Use the name the user gives a feature.

## C#

- No tuples anywhere (types, returns, switch on tuples, dictionary keys): a named record or class.
- One top-level type per file, named as the file (`CodeRulesTests` checks it). Private nested records are fine.
- Named constants instead of magic numbers and strings, and a status or kind is an enum (`enum-status`).
  A staff command's name is its permission: `StaffPermissions`, registered with `commands.AddStaff(...)`.
- Primary constructors, records, pattern matching. An entry a dictionary holds is an immutable record
  (`entries-are-records`). `internal` unless another assembly needs it (the tests see internals).
- `CyrodiilRP.Common` holds only what the client uses too: the RPC contract, `ChatLine`, `ChatLimits`, `TraderOrigin`.
  A page's view (`*View`) and its JSON context (`*Json`) go in the server, beside the service that builds them: the
  client passes the JSON through as a string.
- The update thread owns the entities. Database work goes through `Persistence.Reader` / `Persistence.Writer`, async work
  starts with `Fire.Run` and comes back to the tick. After an await, check `player.IsLive`.
- A handler of an event runs through `Callbacks.Raise`, a timer through `Timers` with a name of its own
  (`timers.RunEvery("traders.keep", ...)`), so one that throws is logged by name and the others still run.
- The container gives a service the clock as `SystemClock` (a `[Service]` is registered by its own type only). A class
  the tests build takes it as `IClock`, so a test passes a `FakeClock` (`Scheduler`, `AskPacing`).
- Texts for players only through the generated `*Text` classes (`ChatText.Say(name, text)`), never a key as a string.
- A refusal is a `Phrase`: `call.Refuse(...)`. What speaks says so (`Speaks` on a command or a wheel's option), and
  the mute's refusal comes from `CommandRegistry.FindSpeechRefusal` (null when allowed).
- A sanction goes through `call.MayPunish(target)`, or `call.Outranks(name, roles.ResolveRoles(...))` for an offline account,
  so the owners of the config are never left out.
- Strings stored in a column are cut with `ColumnLengths.Cut`, which never splits a surrogate pair.
- What the staff do goes to `Audit.Log` with a details record of its own.

## Comments

- One line by default, a block only where `comments` in the standard allows one. Only where the code does not say it
  itself (a rule's reason, a number's source, a platform quirk). No file headers that list what a class does, no
  comments that retell the code, no XML doc comments on internal code.
- No semicolon in the middle of a comment sentence. Never an em-dash, anywhere (code, texts, docs, commit messages).

## Web

- Vue 3 composition API (`<script setup>`), plain JavaScript. Components from `ui/rp`.
- A page talks to the game only through its `bridge.js` (`readym.on("X")`, `readym.sendX(...)`). On the C# side the
  `[Service]` that owns the page declares its messages itself and `Mod.Init` registers it as `ICefRpcHandler`.

## Repo

- Do not commit or deploy unless the user asks.
- Files end lines with LF. A script on Windows keeps them so (Python: `read_bytes` and `write_bytes`, not text mode).
- Never ship an assembly the server or another mod already loads (`pack.ps1` filters them).

See also the `testing`, `game-ui` and `performance` skills.
