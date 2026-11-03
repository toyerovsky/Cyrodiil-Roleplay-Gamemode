import { test, expect, readTexts, screenshot, readSent, expectNoHorizontalOverflow, pressInGame, pauseClock } from "./fixtures.js";

const t = readTexts("ui.welcome");
const KEY_E = 69;
const ENTER = 13;
// The pretend game's wait before the game closes the page.
const ANSWER_MS = 50;

async function expectToFitScreen(page) {
  const fits = await page.locator(".welcome__panel").evaluate((element) => element.scrollHeight <= element.clientHeight + 1);
  expect(fits, "the welcome should not need scrolling").toBe(true);
  await expectNoHorizontalOverflow(page, ".welcome__panel");
}

test("a returning player sees their character and the locked slot, and plays on E", async ({ page }, testInfo) => {
  await page.clock.install();
  await page.goto("/welcome/index.html");

  await expect(page.getByRole("heading", { level: 1, name: "Welcome back, toyer" })).toBeVisible();
  const slots = page.getByRole("list", { name: t("slots") }).getByRole("listitem");
  await expect(slots).toHaveCount(2);
  await expect(slots.nth(0)).toContainText("Marcus Varro");
  await expect(slots.nth(0)).toContainText("7 h 25 min");
  await expect(slots.nth(1)).toContainText(t("locked"));
  await expect(slots.nth(1).getByRole("button")).toHaveCount(0);
  await expect(page.getByRole("button", { name: t("play") })).toBeFocused();
  await expectToFitScreen(page);
  await screenshot(page, testInfo, "welcome-back");

  // As the game hands the welcome a key: with nothing focused.
  await pauseClock(page);
  await page.evaluate(() => document.activeElement?.blur());
  await pressInGame(page, KEY_E);
  await expect(page.getByRole("button", { name: t("entering") })).toBeDisabled();
  await page.clock.runFor(ANSWER_MS);
  await expect(page.locator(".welcome")).toBeHidden();
  expect(await readSent(page, "WelcomePlay")).toHaveLength(1);
  expect(await readSent(page, "WelcomeNew")).toHaveLength(0);
});

test("a first visit is greeted as one and goes on to the name form with New character", async ({ page }, testInfo) => {
  await page.goto("/welcome/index.html#first");

  await expect(page.getByRole("heading", { level: 1, name: t("title_first") })).toBeVisible();
  await expect(page.getByRole("button", { name: t("play") })).toHaveCount(0);
  await expect(page.getByRole("button", { name: t("new") })).toBeFocused();
  await expect(page.getByText(t("keys_new"))).toBeVisible();
  await expectToFitScreen(page);
  await screenshot(page, testInfo, "welcome-first");

  await page.getByRole("button", { name: t("new") }).click();
  await expect(page.locator(".welcome")).toBeHidden();
  expect(await readSent(page, "WelcomeNew")).toHaveLength(1);
});

test("a returning player whose character was never finished starts one on Enter", async ({ page }, testInfo) => {
  await page.goto("/welcome/index.html#empty");

  await expect(page.getByRole("heading", { level: 1, name: "Welcome back, toyer" })).toBeVisible();
  await expect(page.getByRole("button", { name: t("new") })).toBeVisible();
  await expectToFitScreen(page);
  await screenshot(page, testInfo, "welcome-empty");

  await page.evaluate(() => document.activeElement?.blur());
  await pressInGame(page, ENTER);
  await expect(page.locator(".welcome")).toBeHidden();
  expect(await readSent(page, "WelcomeNew")).toHaveLength(1);
});

test("the welcome speaks Polish to a Polish player", async ({ page }, testInfo) => {
  const pl = readTexts("ui.welcome", "pl");
  await page.goto("/welcome/index.html");
  await expect(page.getByRole("button", { name: t("play") })).toBeVisible();
  await page.evaluate(() => window.__readym.emit("Locale", "pl"));

  await expect(page.getByRole("button", { name: pl("play") })).toBeVisible();
  await expect(page.getByText(pl("locked"))).toBeVisible();
  await expectToFitScreen(page);
  await screenshot(page, testInfo, "welcome-pl");
});
