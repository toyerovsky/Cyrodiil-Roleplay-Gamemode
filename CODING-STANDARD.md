# Coding standard

Conventions and invariants for this repo's C# code. These are hard rules. Simple, idiomatic code that a reader
understands without the author: the user reads English as a second language. Every part has a stable id in its
heading: refer to a part by its id, never by its place.

## `names` Full English words
- Full English words: `capabilities`, not `caps`. Accepted short names: `t` (texts), `ct`, `db`, `pg` (the tests'
  database fixture), and one-line lambda parameters (`x => x.Id`).
- Use the name the user gives a feature.

## `csharp` This repo's C#
- No tuples anywhere (types, returns, switch on tuples, dictionary keys): a named record or class.
- One top-level type per file, named as the file (`CodeRulesTests` checks it). Private nested records are fine.
- Named constants instead of magic numbers and strings.
- Primary constructors, records, pattern matching. `internal` unless another assembly needs it (the tests see
  internals).
- Strings stored in a column are cut with `ColumnLengths.Cut`, which never splits a surrogate pair.
- The container gives a service the clock as `SystemClock` (a `[Service]` is registered by its own type only). A class
  the tests build takes it as `IClock`, so a test passes a `FakeClock` (`Scheduler`, `AskPacing`).

## `common` `CyrodiilRP.Common` holds only what the client uses too
`CyrodiilRP.Client.dll` and `CyrodiilRP.Common.dll` go to every player, so Common holds the RPC contract, `ChatLine`,
`ChatLimits`, `TraderOrigin` and nothing only the server uses. A page's view (`*View`) and its JSON context (`*Json`)
go in the server, beside the service that builds them: the client passes the JSON through as a string.

## `update-thread` The update thread owns the entities
- One update thread for every mod. Entities are read and written only on it.
- Database work never runs on it: `Persistence.Reader` (16 reads at once) and `Persistence.Writer` (one queue, in
  order) run it on the pool. Async work starts with `Fire.Run`, which brings the await back to a later tick. After an
  await, check `player.IsLive`.
- A handler of an event runs through `Callbacks.Raise`, a timer through `Timers` with a name of its own
  (`timers.RunEvery("traders.keep", ...)`), so one that throws is logged by name and the others still run.
- All of it runs through `Watchdog.Run`, which logs `[slow] <name> took N ms` past 5 ms, once per 30 s per name. A
  `[slow]` line is a bug to look at. The name is given where the work starts: `timer exchange.watch`, `rpc.pick_wheel
  RadialWheel` (the event, then the class that subscribed), `await accounts.join` (the name `Fire.Run` was given).
- `GameThread` runs only the work queued when the tick began, so work that posts more cannot hold the tick.
- A timer over every player runs only as often as its result can change: play time is written once a minute, expired
  conditions are dropped once a second.
- Anything that must survive a restart is a row with an expiry checked at load, not a timer.

## `sent-to-players` A player gets only what is theirs, when it changes
- Chat lines go only to the players in range (`Proximity`), never to everyone and filtered later.
- A page gets a message on a change, not on a timer. The chat page asks for its history once, when it loads.

## `player-texts` Texts for players come from the generated classes
- Texts for players only through the generated `*Text` classes (`ChatText.Say(name, text)`), never a key as a string.
- A refusal is a `Phrase`: `call.Refuse(...)`. What speaks says so (`Speaks` on a command or a wheel's option), and
  the mute's refusal comes from `CommandRegistry.FindSpeechRefusal` (null when allowed).

## `staff` Staff actions
- A staff command's name is its permission: `StaffPermissions`, registered with `commands.AddStaff(...)`.
- A sanction goes through `call.MayPunish(target)`, or `call.Outranks(name, roles.ResolveRoles(...))` for an offline
  account, so the owners of the config are never left out.
- What the staff do goes to `Audit.Log` with a details record of its own.

## `enum-status` Status is ALWAYS an enum, never a string
There is NO reason to track status/state/kind with strings in C#. This is not JavaScript.

- **Every status/state/kind/mode is a C# `enum`** (or a closed type), never a `string` or a `const string`
  "constants" class. A stringly-typed status lets typos compile, gives no exhaustiveness, and turns `==` into a
  guessing game. A status is a closed set the code branches on; the name of a file, an icon or a text key is not one.
- **Every enum value means something explicit.** Give the enum sensible members; do not leave a "0 = default /
  unknown" hole unless "unknown" is a real, named state.
- **Branch over a status with an EXHAUSTIVE `switch` expression, never if/else chains.** Name every member and leave
  the `_` arm out: a member nobody handles is then a build error (CS8509, an error under `TreatWarningsAsErrors`),
  and a value outside the enum throws `SwitchExpressionException` (CS8524, which only warns about that, is off in
  `Directory.Build.props`). A `_ => throw new ArgumentOutOfRangeException(...)` arm only where a switch cannot name
  every case. A chain of `x == "running" ? ... : x == "done" ? ...` is forbidden.
- **Strings only at the wire boundary** (a page's JSON, an RPC message, a database column), and even there through
  the enum's converter where there is one: `JsonStringEnumConverter` in a page's `*Json` context,
  `HasConversion<string>()` for a column. So the C# side stays typed and the JSON stays readable. Parse inbound
  strings back to the enum immediately; never let a raw status string flow through the code.

## `no-silent-errors` NEVER silently handle an unexpected value
A **silent error** is when code meets a value it did not expect and quietly does something harmless-looking instead
of failing. This is the single most dangerous anti-pattern: it turns a loud, findable bug into a subtle wrong
behavior that hides for weeks. Its classic shape is `Enum.TryParse(...) ? p : SomeValidDefault`, where an
unparseable value silently becomes a legitimate-looking status. These are ALL forbidden, in production code AND
tests:

- **A `switch`/ternary `default`/`_` arm that returns a plausible value** for an unexpected input (e.g. parse
  failure -> a real enum member; unknown case -> "the first one"). If the input is genuinely untrusted and
  out-of-vocabulary, map it to an EXPLICIT distinct value (`Invalid`, `Unrecognized`) that is surfaced and, where it
  flows through our own logic, throw or log at Error. Never fold it into a real member.
- **An `if` that does nothing (or an early `return`/`continue`) when an unexpected value appears.** "It shouldn't
  happen so I'll skip it" is a silent error. Either it truly cannot happen -> `throw` (assert it), or it can ->
  handle it loudly (log at Error/Warn per the ownership rule and take a defined action). A value that is expected,
  if rare, is not one: a player gone after an await is checked with `player.IsLive` and passed over.
- **Skipping a value in a loop with a "this shouldn't appear" comment.** Same thing. In a test especially: do not
  `.Where(x => x != SuspectValue)` it away, iterate EVERY value and `Assert` the exceptional ones (that they throw,
  or return the defined terminal/`Invalid` result). A skipped case is an untested case.
- **A `catch` that swallows** (see `logging`): never empty, never comment-only.

The rule: **every value must be handled EXPLICITLY.** Prefer an EXHAUSTIVE `switch` (expression) that names every
case; a `default`/`_` arm must `throw` (an impossible internal state) or take a real, loud, defined action, never
a quiet no-op or a plausible-looking guess. If a value "can't happen", write the assertion/throw that says so with a
message, so that when it does happen you learn about it immediately.

Distinguish "not set yet" from "set to something we can't read": use SEPARATE explicit enum members (e.g.
`Uninitialized` = absent/never-recorded, vs `Invalid` = present-but-unparseable). Do not collapse them, and do not
use a nullable enum for either. Give each its own defined behavior (e.g. `Uninitialized` -> start fresh; `Invalid`
-> terminal + logged).

## `no-wrong-default` NO default that can be a wrong choice
**A default parameter is only legitimate if it is ALWAYS a correct choice for every caller.** If a default can EVER
be the wrong choice for some caller, it is incorrect and must be REMOVED: make the parameter REQUIRED so every
caller chooses explicitly. A "convenient" default that is right most of the time and silently wrong the rest is a
latent bug.

- The clearest offenders are **timing knobs, modes and flags**: a class that defaulted to the real clock would bring
  the wall clock back into a test that passes a `FakeClock`, so the clock is always passed. Same for any mode, policy
  or flag where the right choice depends on context, and for an optional `null` that lets a caller forget the
  argument (an audit entry's target).
- A default is fine when it is genuinely universal (e.g. an empty collection every caller would want). A no-op
  logger is NOT universal (see `logging`). If you cannot state "this is correct for 100% of callers", delete the
  default.
- **A value in the config is not a parameter.** Its default (`RpConfig.StartingGold`) is the setting a server owner
  gets without writing one, and it stays.

## `config-not-env` Configuration is a FILE, never an environment variable
**What runs on a player's, a server owner's or a developer's machine takes its configuration from a file or the
command line, never from an environment variable, and never with an environment variable as a fallback or an
override** ("the file, unless `FOO` is set"). That covers both halves of the mod (the server's
`server/config.json`), the tests and every script. An environment variable is invisible, differs per machine and per
shell, and gets set wrong on the next machine with nothing to diff against; a file can be read, copied and checked.

- **A secret is the exception.** A password, or a connection string holding one, may reach a tool (the migrator,
  `dotnet ef`) through an environment variable, so that it is not left on a command line or in a shell's history.
- **Out of scope: libraries that read the environment themselves**, such as Testcontainers finding Docker.
- **A service that runs in a container is fine.** It gets its environment from its manifest (`infra/`), which is
  versioned and the same wherever it runs.
- **Passing a value to a subprocess is fine.** A process that sets a variable for a child it launches is making a
  call, not configuring anything: the child reads what its own parent set, never what happens to be in a shell.
- **Not configuration:** OS-provided locations (`APPDATA`, `LOCALAPPDATA`, `USERPROFILE`, `ProgramFiles`, `HOME`)
  and the values a CI runner sets for its own steps.
- **When you find one, remove it in a change of its own,** not folded into whatever you were doing.

## `entries-are-records` An ENTRY is an immutable record
An entry is the thing a registry or a dictionary holds: a few values about one item, with no behaviour of its own.
**An entry is an immutable `record`**, changed by putting a new one in its place:

```csharp
_plates[player] = plate with { Shown = true };
```

- **A `readonly record struct` where it changes every tick**, so the new one costs no allocation.
- **Never a class with settable properties, never a mutable struct.** A class lets any holder of a reference change
  the entry behind its registry's back. A struct comes out of a dictionary as a copy, so a change made to it is lost
  without a word, a silent error of exactly the kind `no-silent-errors` forbids.
- **A thing with behaviour of its own is not an entry.** It is a class named for what it does.

## `comments` A block comment is EARNED
**One line by default.** A block comment is earned by exactly three things:

- **a lock**, and it must say what it protects, who produces, who consumes, and what is atomic with respect to
  what. A lock whose comment cannot answer all four is the wrong fix, and goes along with the sharing it
  implied;
- **a thread-affinity statement at the top of a class**, stated once there rather than on every method;
- **an external constraint that cost measured time to find**, from the game, the platform, CEF, the database or a
  library, in one short paragraph, at the one place it binds.

Everything else belongs in docs or nowhere. A constant does not get a paragraph. A method whose name says what
it does gets no comment at all. No file headers that list what a class does, no XML doc comments on internal code,
no semicolon in the middle of a comment sentence.

**A comment explains the code under it, and nothing else.** Not the feature it belongs to, not the alternative
that was rejected, not what the code used to be, not when a bug was found or who found it. Those are a
changelog, a design document and a plan, and each already has a place. A comment is for implementation that is
not self-evident.

**NEVER cite a plan, a note or anything else that will not last.** A comment pointing at one becomes a dangling
reference to something nobody can read. Durable docs are citable: this standard, `DEVELOPMENT.md` and the skills
under `.claude/skills/`.

**There is no percentage target.** Comments still count fully towards any line budget, so an unearned one still
costs. This rule is enforced by reading.

## `registry-naming` A registry is ALWAYS named `<Something>Registry`
A collection kept in memory that owns entries and hands them out by key is a REGISTRY, and it is named one. Always.
There is no second word for it: not manager, not book, not table, not store, not cache, not host.

- **The type is `<Something>Registry`,** where `Something` names one entry. `CommandRegistry` holds commands.
- **A registry and its entry are named so the relationship is obvious.** `XyzRegistry` holds `Xyz`. If the entry
  type cannot carry that name, it is an INNER type of the registry that the API never hands out. What is
  forbidden is a registry whose entries are called something unrelated to it, because then nothing about either
  name says they belong together.
- **A bare `Dictionary<SomeId, ...>` member becomes a registry once it carries rules of its own** (how long an
  entry lives, what is refused while one is live) **or sits next to another one.** Two of them side by side in one
  class is the shape this rule exists to catch: the lookups and the lifetime rules end up spread through the owner
  instead of in the thing that owns them. A plain lookup a service keeps for itself may stay a field.
- **What reads and writes the database is not a registry.** It owns no entries, the database does: the `*Book`
  types of `CyrodiilRP.Data` keep their name.
- **The owner holds registries, it does not BE them.** A class with a registry inside it is described by what it
  is for, not by the collections it happens to hold.

## `types-are-nouns` A type is named with a NOUN, never an adjective
A type is a THING. An adjective names a property something HAS, so a reader has to guess what the thing itself is
before they can use it.

- **Classes and interfaces are nouns.** `Watchdog`, not `Watching`; `IClock`, not `ITimed`. Where the type is an
  agent, the agent noun says so: `DatabaseWriter` writes.
- **A plural is a name when the thing is many:** a service that looks after many of a thing (`Parties`), a static
  set of constants or rules (`ChatLimits`, `PartyRules`), a record whose noun is plural itself (`AuditDetails`), a
  test class (`<Subject>Tests`, as xUnit names them). A collection handing entries out by key is not one of them: it
  is a registry and named one (`registry-naming`).
- **The .NET framework's own adjective interfaces are the exception, not the pattern.** `IDisposable` and
  `IEnumerable` are capabilities the runtime and the language understand. Ours are not, so ours are nouns.

## `name-the-dependency` Name what a thing DEPENDS on, not where it sits
A project's or a folder's name does not carry into every type in it, and a type that reads as portable must be
portable.

- **A type that depends on the game, CEF or the database says so** where its plain name would read as though it
  could live without them, which is a lie a reader acts on.
- **A type that depends on nothing of the kind keeps its plain name, even inside a folder about one.** A type that
  holds records and timestamps is named for them. Marking it would be marking the folder rather than the fact.
- **Name the real dependency**, the one a reader needs to know about.

## `logging` Logging & exceptions
These are hard rules for all non-console-app code (and tests).

- **Use `Microsoft.Extensions.Logging` (`ILogger`).** Do NOT hand-roll logging seams (no `Action<string> log`
  delegates). Convert `Console.*` to `ILogger` everywhere that is NOT a console application's own CLI output. Do NOT
  use `Console.Error` anywhere, including tests.
- **The logger is ALWAYS specified.** Inject `ILogger` as a REQUIRED constructor parameter (no optional
  `ILogger? = null`). NEVER use `NullLogger`. A component that can be constructed without a logger is a bug.
- **Never swallow an exception. NEVER.** Every `catch` either rethrows or logs (message + context). Empty or
  comment-only catch blocks are forbidden: a comment is not handling. This holds even for routine cancellation: a
  `catch (OperationCanceledException)` on shutdown still writes a `LogDebug` (never a bare `{ }`).
- **A BROAD `catch (Exception ex)` MUST log at Error, never Warning.** Catching an arbitrary exception means an
  UNEXPECTED condition (our own broken state), Error-grade even when you recover. If a condition is genuinely
  routine and deserves Warning, catch the SPECIFIC narrow exception type for it and Warn on that. "Broad catch ->
  Warning" is always a bug.
- **Abnormal situations are Errors, EVEN WHEN gracefully recovered.** The level is set by NORMAL vs ABNORMAL, not by
  whether it was handled. Corrupt/impossible/unexpected state is ABNORMAL: log at **Error** (`LogError(ex, ...)`) so
  it FAILS tests and is loud in prod, even though you recover (fall back, skip, return failure). "The app keeps
  running" is not a reason to downgrade to Warning.
- **Error is for OUR OWN broken state; not for what someone else did.** Reserve Error for conditions that mean OUR
  code/state is wrong (corrupt state we wrote, an impossible internal invariant, a bug). **Bad EXTERNAL input is a
  Warning, not an Error**, even though it is abnormal: a malformed message from a page, a client sending garbage. We
  handle it gracefully (drop/reject) and Warn. A player's mistyped command is neither: it is refused, not logged.
  If in doubt whether an abnormal condition is OUR fault, treat it as Error; if it is clearly external input, Warn.
- **Cancellation is the routine case.** An `OperationCanceledException`/`TaskCanceledException` from expected
  shutdown is logged below Error (Debug/Information). It is still LOGGED, never a bare or comment-only catch.
- **Never drop the exception argument.** Pass the exception object (`logger.LogError(ex, "...")`), not just
  `ex.Message`. Use structured message templates (named placeholders), not interpolated strings.
- **In TESTS, NEVER catch and swallow.** A test that catches must ASSERT it (e.g.
  `await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run)`), never `try { ... } catch { }`. Tests fill
  the `ILogger` dependency with a fail-on-error logger (throws on `LogLevel >= Error`), so an unexpected error log
  fails the test. A test that checks an error is logged tells the logger to expect it first.
