---
name: ui-playwright
description: Preview the Cyrodiil RP pages without the game and verify them with headless Playwright, screenshots and pretend-game scenarios. Use for UI changes and browser regression checks.
---

# Browser preview and Playwright

Run from `ui/`. Only Node.js 22+ is needed.

```powershell
npm ci
npx playwright install chromium
npm run dev
```

`http://127.0.0.1:5173/` is the gallery: every page with its scenarios and the design system's components. A page runs
on a pretend game (`<page>/pretend.js`) through the stand-in bridge `dev/readym.js`, which keeps what the page sent in
`window.__readym.sent` and plays the game with `window.__readym.emit(name, ...args)`. Scenarios are the hash
(`chat/index.html#long`); a change of the hash alone does not reload the page.

## Running

```powershell
npm run test:e2e -- e2e/chat.spec.js                          # one spec, 1920x1080
$env:E2E_SIZES='all'; npm run test:e2e -- e2e/chat.spec.js    # at 1366x768, 1920x1080, 1280x1024, 2560x1080
npm run test:e2e:report
```

Playwright starts its own Vite on port 5174 (`E2E_PORT`), `E2E_WORKERS` sets the browsers at once. Results go to
`ui/test-results/` and `ui/playwright-report/`.

## Writing a spec

- Use `e2e/fixtures.js`: every test fails on a page error or a missing file.
- Drive the page with roles, labels and keys, assert what the player sees and what the page sent (`readSent(page, "Submit")`).
  Use Playwright's waiting assertions, `page.clock` for timers, never fixed sleeps.
- A state that lasts only until the pretend game answers (a button that says it is waiting) is asserted on a paused
  clock: `page.clock.install()` before `goto`, `pauseClock(page)` once the page shows, then `page.clock.runFor(ms)` for
  the answer. With the clock running, the answer can come before the assertion looks.
- A scenario is the hash of the first `goto`. A query (`?shot=`) is only for a second `goto` of the same page in one
  test, which a change of the hash alone would not reload.
- `screenshot(page, testInfo, name)` at the states that matter, then open the PNGs and look at them, the smallest text at
  1366x768 first. `expectNoHorizontalOverflow` on a panel.
- Headless Chromium hides scrollbars: check a scrolling list by its `scrollTop`, not by the screenshot.
- `gallery.spec.js` renders every scenario of `pages.js`. A changed behaviour gets a spec of its own.
- `npm run build` runs `scripts/check-dist.mjs`: every page built, nothing of the preview shipped, the size budget kept.

Report which scenarios and sizes were checked and what needs a check in the game.
