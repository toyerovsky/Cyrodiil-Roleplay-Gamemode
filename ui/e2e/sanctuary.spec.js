import { test, expect, readTexts, screenshot, pauseClock } from "./fixtures.js";

const t = readTexts("ui.sanctuary");

// The seconds the badge shows as left: "4:47 left" is 287.
async function readLeft(badge) {
  const [minutes, seconds] = (await badge.locator(".sanctuary__left").textContent()).match(/(\d+):(\d+)/).slice(1).map(Number);
  return minutes * 60 + seconds;
}

test("the badge shows a protected player inside the Sanctuary and lets the mouse through", async ({ page }, testInfo) => {
  await page.goto("/sanctuary/index.html");
  const badge = page.getByRole("status", { name: t("title") });
  await expect(badge).toContainText(t("protected"));
  await expect(badge.locator(".sanctuary__left")).toHaveCount(0);
  expect(await page.evaluate(() => getComputedStyle(document.body).pointerEvents)).toBe("none");
  const inside = await badge.evaluate((element) => {
    const box = element.getBoundingClientRect();
    return box.right <= window.innerWidth && box.top >= 0;
  });
  expect(inside).toBe(true);
  await screenshot(page, testInfo, "sanctuary-protected");
});

test("an aggressor's badge counts the streak down and goes when the player leaves", async ({ page }, testInfo) => {
  await page.clock.install();
  await page.goto("/sanctuary/index.html#aggressor");
  const badge = page.getByRole("status", { name: t("title") });
  await expect(badge).toContainText(t("aggressor"));
  await expect(badge.locator(".sanctuary__left")).toHaveText(/^4:\d\d left$/);
  await pauseClock(page);
  const before = await readLeft(badge);
  await page.clock.runFor(10_000);
  expect(await readLeft(badge)).toBe(before - 10);
  await screenshot(page, testInfo, "sanctuary-aggressor");

  await page.evaluate(() => window.__readym.emit("State", JSON.stringify({ inside: false, aggressor: true, secondsLeft: 200 })));
  await expect(badge).toHaveCount(0);
});

test("the badge reads in Polish", async ({ page }, testInfo) => {
  const pl = readTexts("ui.sanctuary", "pl");
  await page.goto("/sanctuary/index.html#aggressor");
  await page.evaluate(() => window.__readym.emit("Locale", "pl"));
  const badge = page.getByRole("status", { name: pl("title") });
  await expect(badge).toContainText(pl("aggressor"));
  await screenshot(page, testInfo, "sanctuary-polish");
});
