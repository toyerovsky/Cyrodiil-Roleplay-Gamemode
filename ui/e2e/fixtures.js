import { readFileSync } from "node:fs";
import { test as base, expect } from "@playwright/test";

// Every test fails on a page error or a missing file, whatever it asserts itself.
export const test = base.extend({
  page: async ({ page }, use) => {
    const errors = [];
    page.on("pageerror", (error) => errors.push(error.message));
    page.on("response", (response) => {
      if (response.status() >= 400) errors.push(`${response.status()} ${response.url()}`);
    });
    await use(page);
    expect(errors, "Browser errors and missing files").toEqual([]);
  },
});
export { expect };

export function readTexts(group, locale = "en") {
  const all = JSON.parse(readFileSync(new URL(`../../locales/${locale}/${group}.json`, import.meta.url), "utf8"));
  const prefix = `${group}.`;
  return (key) => all[prefix + key];
}

export async function screenshot(page, testInfo, name) {
  await page.evaluate(() => document.fonts.ready);
  const path = testInfo.outputPath(`${name}.png`);
  await page.screenshot({ path, animations: "disabled" });
  await testInfo.attach(name, { path, contentType: "image/png" });
}

// After clock.install() and once the page shows: from here on the page's timers wait for clock.runFor, so an answer the
// pretend game sends a moment later cannot come before the test looked at what the page shows meanwhile.
const PAUSE_AHEAD_MS = 1000;
export async function pauseClock(page) {
  const now = await page.evaluate(() => Date.now());
  await page.clock.pauseAt(now + PAUSE_AHEAD_MS);
}

export async function readSent(page, name) {
  return page.evaluate((wanted) => (window.__readym?.sent ?? []).filter((m) => m.name === wanted).map((m) => m.args), name);
}

export async function expectNoHorizontalOverflow(page, selector) {
  const overflow = await page.locator(selector).evaluate((element) => element.scrollWidth > element.clientWidth + 1);
  expect(overflow, `${selector} should not scroll sideways`).toBe(false);
}

// A key as the game sends it to its pages: the Windows key code alone, no scan code, so the page sees no `code`.
export async function pressInGame(page, windowsVirtualKeyCode) {
  const cdp = await page.context().newCDPSession(page);
  for (const type of ["rawKeyDown", "keyUp"])
    await cdp.send("Input.dispatchKeyEvent", { type, windowsVirtualKeyCode, nativeVirtualKeyCode: 0, code: "", key: "" });
  await cdp.detach();
}
