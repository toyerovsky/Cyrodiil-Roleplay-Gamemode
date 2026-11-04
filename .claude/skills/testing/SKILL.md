---
name: testing
description: How to write and run the tests of Cyrodiil RP (C# xUnit with PostgreSQL, Vue Vitest, Playwright, the real server). Use when changing code and before handing work back.
---

# Testing

## When to run what

- **While working:** the tests of the area you change.
  `dotnet test tests/CyrodiilRP.Tests --filter "FullyQualifiedName~CharacterBookTests"`, `cd ui; npx vitest run tests/chat.test.js`.
- **At the end of a task:** the tests of your area and of everything that uses what you changed, the E2E spec of a page
  you changed at the four sizes (`ui-playwright`) and `npm run build` after a UI change.
- **Before a commit:** `dotnet test tests/CyrodiilRP.Tests --filter "Category!=Server"` (Docker must run), `cd ui; npx vitest run;
  npm run build; npm run test:e2e`, then `./pack.ps1 -Test -PackOnly`, which runs the C# tests and the server smoke
  tests on the fresh package.
- A failure in a full run gets a filtered rerun while fixing, then one full run again.

## The C# suite

- A folder per server folder (`Characters/`, `Exchange/`, `Party/`, `Traders/`, ...) for its rules, `Infrastructure/`,
  `Localization/`, and `Rules/` for the rules of commands and staff and the repository's own: `Rules/CodeRulesTests`
  checks LF endings, no em-dash and one type per file across the repo. A class there that needs a database takes
  `PostgresFixture` as `Database/` does.
- `Database/`: every class gets its own database, cloned from a template migrated once, on one PostgreSQL in Docker
  (`PostgresFixture`, `IClassFixture<PostgresFixture>`). Classes run in parallel. A test makes its own rows
  (`Seeds.CreateAccountAsync`, `Seeds.CreateCharacterAsync`, `Names.CreateUnique()`) and never leans on another test's.
- Every database step on a context of its own (`pg.RunAsync(db => ...)`), as the writer gives every job: a reused
  context hands back what it tracks, and the test passes for the wrong reason.
- `Server/`: the packed mod (`out/mods/CyrodiilRP`) on a copy of the real server and a database of its own. It
  fails on any `[ERR]` or `[FTL]` line, the first seconds after start included. The newest `oblivion-server-*` folder is
  used, or `OBLIVIONMP_SERVER`.
- `Database/MigratorTests` runs the built migrator as a process, `Localization/TextsGeneratorTests` the texts generator
  on locale files of the test's own (`Fakes/LocaleText`).
- Time: `FakeClock` for timers (`Scheduler`), `Seeds.Now` for rows.
- Logs: `ListLogger` and `.Errors`, so a test sees what the watchdog logged.

## Writing tests

- Every change of behaviour comes with a test that fails without it. A suspected bug is shown by a test first.
- One behaviour per test, named as a sentence (`A_ban_comes_back_at_the_next_join_until_it_is_lifted`).
- Numbers come from the code's constants (`CommandLine.MaxMinutes`, `ColumnLengths.Reason`), not literals.
- A constraint test names the constraint the database refuses with (`AssertRefusedByAsync("ck_accounts_ban", ...)`).
- UI: Vitest mounts the real page on its real `bridge.js` over a fake `readym` (`tests/readym.js`: `game.emit(name, ...)`
  plays the game, `game.readSent(name)` is what the page sent), and unmounts it after each test. After a key or a click
  the page reacts to, `await flushPromises()` (`@vue/test-utils`), not a message the game never sends.
  `tests/contract.test.js` ties every `bridge.js` to its C# class, `tests/limits.test.js` the fields to the server's
  limits. A test that only reads files runs with `// @vitest-environment node`.

See also the `code-rules` and `ui-playwright` skills.
