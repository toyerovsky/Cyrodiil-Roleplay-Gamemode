import { test, expect, screenshot, expectNoHorizontalOverflow, readSent } from "./fixtures.js";

test("a playing character gains XP, levels up, reaches the cap and gives the mouse to the game", async ({ page }, testInfo) => {
  await page.goto("/progression/index.html");
  const panel = page.getByRole("region", { name: "Experience" });
  await expect(panel).toContainText("250 / 800 XP");
  expect(await readSent(page, "ProgressionReady")).toEqual([[]]);
  expect(await page.evaluate(() => getComputedStyle(document.body).pointerEvents)).toBe("none");
  await expectNoHorizontalOverflow(page, ".progression");
  await screenshot(page, testInfo, "level-one");
  await page.evaluate(() => window.__readym.emit("State", JSON.stringify({ enabled: true, level: 12, xp: 1200, next: 3100, perkPoints: 11 })));
  await expect(panel).toContainText("Level 12");
  await expect(panel).toContainText("Perk points: 11");
  await screenshot(page, testInfo, "level-up");
  await page.evaluate(() => {
    window.__readym.emit("Locale", "pl");
    window.__readym.emit("State", JSON.stringify({ enabled: true, level: 40, xp: 0, next: 0, perkPoints: 39 }));
  });
  await expect(page.getByRole("region")).toContainText("Maksymalny poziom");
  await expect(page.getByRole("progressbar")).toHaveAttribute("aria-valuenow", "1");
  await expectNoHorizontalOverflow(page, ".progression");
  await screenshot(page, testInfo, "maximum-polish");
  await page.evaluate(() => window.__readym.emit("State", JSON.stringify({ enabled: false })));
  await expect(page.getByRole("progressbar")).toHaveCount(0);
});
