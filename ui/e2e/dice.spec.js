import { test, expect, readTexts, screenshot, readSent, expectNoHorizontalOverflow, pressInGame } from "./fixtures.js";

const t = readTexts("ui.dice");

const SPACE = 32;
const DIGIT_THREE = 51;
// A throw takes the windup, the flight and the settling: a few seconds in a headless browser that draws in software.
const LANDING_MS = 20_000;

// Each throw is drawn in software here, so a test of two throws needs more than the usual half minute.
test.describe.configure({ timeout: 90_000 });

const landed = (page, count) => expect.poll(() => page.evaluate(() => window.__diceBoard?.throws ?? 0), { timeout: LANDING_MS }).toBe(count);
const lastThrow = (page) => page.evaluate(() => window.__diceBoard.last);
// The board's page brings three.js and its models: in a busy preview server it takes longer than the usual few seconds.
const PAGE_MS = 20_000;
const trayOf = (page) => page.getByRole("region", { name: t("tray") });
const opened = (page) => expect(trayOf(page)).toBeVisible({ timeout: PAGE_MS });

test("the board opens on its tray, picks dice and a set with the mouse and throws them with the button", async ({ page }, testInfo) => {
  await page.goto("/dice/index.html");
  await opened(page);
  await expect(page.getByRole("button", { name: `Space ${t("throw")}` })).toBeFocused();
  await expect(page.locator(".dice__roll")).toHaveCount(4);
  await expect(page.locator(".dice__asked b")).toHaveText("1d20");
  await expect(page.locator(".dice__asked small")).toHaveText(t("placeholder"));
  await expectNoHorizontalOverflow(page, ".dice__panel--tray");
  await expectNoHorizontalOverflow(page, ".dice__panel--journal");
  await screenshot(page, testInfo, "dice-open");

  const more = (die) => page.getByRole("button", { name: t("more").replace("{die}", die) });
  await more("d6").click();
  await more("d6").click();
  await more("d20").click();
  await expect(page.locator(".dice__asked b")).toHaveText("2d6 + 1d20");
  await page.getByRole("button", { name: t("fewer").replace("{die}", "d6") }).click();
  await expect(page.locator(".dice__asked b")).toHaveText("1d6 + 1d20");

  await page.getByRole("button", { name: t("set.daedric") }).click();
  await expect(page.getByRole("button", { name: t("set.daedric") })).toHaveAttribute("aria-pressed", "true");
  await expect(page.getByText(t("set_text.daedric"))).toBeVisible();
  expect(await readSent(page, "DiceSet")).toEqual([["daedric"]]);

  await page.getByRole("button", { name: `Space ${t("throw")}` }).click();
  expect(await readSent(page, "DiceThrow")).toEqual([[1, 1, 0, 1, 0]]);
  await landed(page, 1);
  const thrown = await lastThrow(page);
  expect(thrown.landed).toEqual(thrown.expected);
  expect(thrown.sets).toEqual(["daedric", "daedric"]);
  await expect(page.locator(".dice__banner")).toContainText("1d6 + 1d20");
  await expect(page.locator(".dice__roll")).toHaveCount(5);
  await screenshot(page, testInfo, "dice-thrown");
});

test("the keys throw, add, clear, turn the board dark and close it", async ({ page }, testInfo) => {
  await page.goto("/dice/index.html#empty");
  await opened(page);
  await expect(page.getByText(t("recent_none"))).toBeVisible();

  await pressInGame(page, DIGIT_THREE);
  await pressInGame(page, DIGIT_THREE);
  await expect(page.locator(".dice__asked b")).toHaveText("2d20");
  await page.keyboard.press("ArrowUp");
  await page.keyboard.press("ArrowRight");
  await expect(page.locator(".dice__asked b")).toHaveText("1d10 + 2d20");
  await page.keyboard.press("5");
  await expect(page.locator(".dice__asked b")).toHaveText("1d20");
  for (const _ of [1, 2, 3, 4, 5]) await page.keyboard.press("2");
  await page.keyboard.press("2");
  await expect(page.locator(".dice__asked small")).toHaveText(t("too_many").replace("{max}", "5"));

  await page.keyboard.press("v");
  await expect(page.getByRole("button", { name: t("dark") })).toHaveAttribute("aria-pressed", "true");
  await page.keyboard.press("m");
  await expect(page.getByRole("button", { name: t("sound") })).toHaveAttribute("aria-pressed", "false");

  await pressInGame(page, SPACE);
  expect(await readSent(page, "DiceThrow")).toEqual([[1, 0, 5, 0, 0]]);
  await landed(page, 1);
  expect((await lastThrow(page)).landed).toHaveLength(5);
  await expect(page.locator(".dice__roll")).toHaveCount(1);
  await screenshot(page, testInfo, "dice-dark-five");

  await page.keyboard.press("Escape");
  expect(await readSent(page, "DiceClose")).toEqual([[]]);
  await expect(page.getByRole("region", { name: t("tray") })).toBeHidden();
});

test("a natural 20 lands crowned, and /roll with the board open throws the dice it named", async ({ page }, testInfo) => {
  await page.goto("/dice/index.html#crit");
  await opened(page);
  await pressInGame(page, SPACE);
  await landed(page, 1);
  await expect(page.locator(".dice__banner")).toContainText(t("natural_20"));
  await expect(page.locator(".dice-tag--crit")).toHaveText("20");
  await screenshot(page, testInfo, "dice-natural-20");

  await page.evaluate(() => window.__readym.emit("ThrowOnBoard", 0, 2, 0, 1));
  await expect(page.locator(".dice__asked b")).toHaveText("2d10 + 1d100");
  expect((await readSent(page, "DiceThrow")).at(-1)).toEqual([2, 0, 2, 0, 1]);
  await landed(page, 2);
  expect((await lastThrow(page)).landed).toEqual([10, 10, 100]);
});

test("a refused throw settles back and says why", async ({ page }, testInfo) => {
  await page.goto("/dice/index.html#refuse");
  await opened(page);
  await page.getByRole("button", { name: `Space ${t("throw")}` }).click();
  await expect(page.locator(".dice__banner--warn")).toHaveText("The dice are still rolling.");
  await expect(page.getByRole("button", { name: `Space ${t("throw")}` })).toBeEnabled();
  await screenshot(page, testInfo, "dice-refused");
});

test("a roll made for something else lands on a small board that takes no click, then goes", async ({ page }, testInfo) => {
  await page.goto("/dice/index.html#present");
  const caption = page.locator(".dice__caption");
  await expect(caption).toContainText("Lazy", { timeout: PAGE_MS });
  await expect(page.getByRole("region", { name: t("tray") })).toHaveCount(0);
  await landed(page, 1);
  await expect(caption).toContainText("Lazy: 5d20 = ");
  const thrown = await lastThrow(page);
  expect(thrown.landed).toEqual(thrown.expected);
  expect(new Set(thrown.sets).size).toBe(5);
  expect(await page.locator(".dice").evaluate((element) => getComputedStyle(element).pointerEvents)).toBe("none");
  await screenshot(page, testInfo, "dice-present");

  await expect.poll(() => readSent(page, "DicePresented"), { timeout: LANDING_MS }).toEqual([[]]);
  await expect(caption).toHaveCount(0);
});
