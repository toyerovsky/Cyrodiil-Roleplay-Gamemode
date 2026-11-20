import { test, expect, readTexts, screenshot, readSent } from "./fixtures.js";

const t = readTexts("ui.away");
const START = new Date("2026-11-20T12:00:00Z");
const ANSWER_MS = 100;

// The clock stands still from before the page loads, so the screen counts only as the test moves it.
async function openScreen(page, url) {
  await page.clock.install({ time: START });
  await page.clock.pauseAt(new Date(START.getTime() + ANSWER_MS));
  await page.goto(url);
  await expect.poll(() => readSent(page, "AwayReady")).toHaveLength(1);
  await page.clock.runFor(ANSWER_MS);
  await expect(page.getByRole("status")).toBeVisible();
}

test("a still player is asked whether they are there, with the seconds left before the mark", async ({ page }, testInfo) => {
  await openScreen(page, "/away/index.html");
  await expect(page.getByRole("heading", { name: t("warning_title") })).toBeVisible();
  await expect(page.locator(".away__time")).toHaveText("0:30");
  await expect(page.locator(".away__hint")).toHaveText(t("warning_hint"));
  await screenshot(page, testInfo, "away-warning");

  await page.clock.runFor(3000);
  await expect(page.locator(".away__time")).toHaveText("0:27");
});

test("a player marked away reads it, how long and that others see it", async ({ page }, testInfo) => {
  await openScreen(page, "/away/index.html#away");
  await expect(page.getByRole("heading", { name: t("away_title") })).toBeVisible();
  await expect(page.locator(".away__time")).toHaveText("4:51");
  await expect(page.locator(".away__seen")).toHaveText(t("away_seen"));
  await screenshot(page, testInfo, "away-marked");

  await page.clock.runFor(10_000);
  await expect(page.locator(".away__time")).toHaveText("5:01");
});

test("an hour away shows the hours, and the screen never takes a click from the game", async ({ page }, testInfo) => {
  await page.goto("/away/index.html#long");
  await expect(page.locator(".away__time")).toHaveText(/^1:02:0\d$/);
  const takesClicks = await page.evaluate(() =>
    [document.body, document.querySelector(".away"), document.querySelector(".away__head")].some(
      (element) => getComputedStyle(element).pointerEvents !== "none",
    ),
  );
  expect(takesClicks).toBe(false);
  await screenshot(page, testInfo, "away-long");
});

test("the screen goes the moment the player is back", async ({ page }) => {
  await page.goto("/away/index.html#away");
  await expect(page.getByRole("status")).toBeVisible();
  await page.evaluate(() => window.__readym.emit("Ended"));
  await expect(page.getByRole("status")).toHaveCount(0);
});
