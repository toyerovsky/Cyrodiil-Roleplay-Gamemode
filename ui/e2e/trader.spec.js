import { test, expect, readTexts, screenshot, readSent, expectNoHorizontalOverflow } from "./fixtures.js";

const t = readTexts("ui.trader");

test("a ware is picked, counted and bought, and the purse follows", async ({ page }, testInfo) => {
  await page.goto("/trader/index.html");
  const window = page.getByRole("region", { name: t("shop.general") });
  const wares = page.getByRole("listbox", { name: t("wares") });
  await expect(window).toBeVisible();
  await expect(wares).toBeFocused();
  await expect(page.getByRole("option", { name: /Bread Loaf/ })).toHaveAttribute("aria-selected", "true");
  await expectNoHorizontalOverflow(page, ".trader__panel");
  await screenshot(page, testInfo, "trader-open");

  await page.getByRole("option", { name: /Iron Longsword/ }).click();
  await page.getByRole("button", { name: t("more") }).click();
  await expect(page.locator(".trader__total")).toHaveText("Total 56 gold");
  await page.getByRole("button", { name: t("buy"), exact: true }).click();
  await expect(page.locator(".trader__status")).toHaveText("Bought Iron Longsword × 2. Paid 56 gold coins.");
  await expect(page.locator(".trader__purse")).toContainText("64 gold");
  await screenshot(page, testInfo, "trader-bought");
  expect(await readSent(page, "TraderBuy")).toEqual([["WeapIronLongsword", 2]]);
});

test("a kind of wares at a time, from the keyboard too", async ({ page }, testInfo) => {
  await page.goto("/trader/index.html");
  const kinds = page.getByRole("navigation", { name: t("categories") });
  await expect(kinds.getByRole("button", { name: t("all"), pressed: true })).toBeVisible();
  await kinds.getByRole("button", { name: t("category.weapons") }).focus();
  await page.keyboard.press("Space");
  await expect(kinds.getByRole("button", { name: t("category.weapons"), pressed: true })).toBeVisible();
  await expect(kinds.getByRole("button", { pressed: true })).toHaveCount(1);
  await expect(page.getByRole("option")).toHaveCount(4);
  await screenshot(page, testInfo, "trader-weapons");
  await page.getByRole("listbox", { name: t("wares") }).focus();
  await page.keyboard.press("End");
  await expect(page.getByRole("option", { name: /Iron Arrow/ })).toHaveAttribute("aria-selected", "true");
  await page.keyboard.press("+");
  await page.keyboard.press("+");
  await page.keyboard.press("Enter");
  expect(await readSent(page, "TraderBuy")).toEqual([["Arrow1Iron", 3]]);
});

test("with a thin purse the dear wares are marked and cannot be bought", async ({ page }, testInfo) => {
  await page.goto("/trader/index.html#poor");
  await page.getByRole("option", { name: /Iron Longsword/ }).click();
  await expect(page.getByRole("button", { name: t("too_poor") })).toBeDisabled();
  await screenshot(page, testInfo, "trader-poor");
});

test("Escape leaves the trader", async ({ page }) => {
  await page.goto("/trader/index.html");
  await expect(page.getByRole("listbox", { name: t("wares") })).toBeFocused();
  await page.keyboard.press("Escape");
  await expect(page.locator(".trader")).toHaveCount(0);
  expect(await readSent(page, "TraderClose")).toEqual([[]]);
});
