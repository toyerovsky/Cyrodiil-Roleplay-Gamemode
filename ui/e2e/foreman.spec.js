import { test, expect, readTexts, screenshot, readSent, expectNoHorizontalOverflow } from "./fixtures.js";

const t = readTexts("ui.foreman");

test("the lumber camp leader sells the axe, and the purse follows", async ({ page }, testInfo) => {
  await page.goto("/foreman/index.html");
  const window = page.getByRole("region", { name: "Lumber Camp Leader" });
  const forSale = page.getByRole("listbox", { name: t("for_sale") });
  await expect(window).toBeVisible();
  await expect(forSale).toBeFocused();
  await expect(page.getByRole("option", { name: /Lumber Axe/ })).toHaveAttribute("aria-selected", "true");
  await expectNoHorizontalOverflow(page, ".trader__panel");
  await screenshot(page, testInfo, "foreman-open");

  await page.getByRole("button", { name: t("buy"), exact: true }).click();
  await expect(page.locator(".trader__status")).toHaveText("You buy 1 Lumber Axe for 50 gold.");
  await expect(page.locator(".trader__purse")).toContainText("10 gold");
  await expect(page.getByRole("button", { name: t("too_poor") })).toBeDisabled();
  await screenshot(page, testInfo, "foreman-bought");
  expect(await readSent(page, "ForemanDeal")).toEqual([["buy:0", 1]]);
});

test("firewood is sold by the count, from the keyboard too", async ({ page }, testInfo) => {
  await page.goto("/foreman/index.html");
  const kinds = page.getByRole("navigation", { name: t("kinds") });
  await kinds.getByRole("button", { name: t("wanted") }).focus();
  await page.keyboard.press("Space");
  await expect(kinds.getByRole("button", { name: t("wanted"), pressed: true })).toBeVisible();
  const wanted = page.getByRole("listbox", { name: t("wanted") });
  await expect(page.getByRole("option", { name: /Firewood/ })).toHaveAttribute("aria-selected", "true");
  await expect(page.locator(".trader__detail")).toContainText("You carry 12");
  await screenshot(page, testInfo, "foreman-wanted");

  await wanted.focus();
  for (let press = 0; press < 4; press += 1) await page.keyboard.press("+");
  await expect(page.locator(".trader__total")).toHaveText("You get 5 gold");
  await page.keyboard.press("Enter");
  await expect(page.locator(".trader__status")).toHaveText("You sell 5 Firewood for 5 gold.");
  await expect(page.locator(".trader__detail")).toContainText("You carry 7");
  expect(await readSent(page, "ForemanDeal")).toEqual([["sell:Firewood", 5]]);
});

test("too little gold for the warhammer, but stone still sells", async ({ page }, testInfo) => {
  await page.goto("/foreman/index.html#quarry");
  await expect(page.getByRole("option", { name: /Iron Warhammer/ })).toHaveAttribute("aria-selected", "true");
  await expect(page.getByRole("button", { name: t("too_poor") })).toBeDisabled();
  await expect(page.getByRole("button", { name: t("more") })).toBeDisabled();
  await screenshot(page, testInfo, "foreman-poor");
});

test("the dock foreman pays the wages owed, once", async ({ page }, testInfo) => {
  await page.goto("/foreman/index.html#docks");
  const wages = page.getByRole("region", { name: t("wages") });
  await expect(wages).toContainText("Owed to you: 7 gold");
  await expect(page.getByRole("listbox")).toHaveCount(0);
  await expectNoHorizontalOverflow(page, ".trader__panel");
  await screenshot(page, testInfo, "foreman-docks");

  await page.getByRole("button", { name: t("collect") }).click();
  await expect(page.locator(".trader__status")).toHaveText("The foreman pays you 7 gold for 7 crates.");
  await expect(page.getByRole("button", { name: t("nothing_owed") })).toBeDisabled();
  await expect(page.locator(".trader__purse")).toContainText("10 gold");
  await screenshot(page, testInfo, "foreman-paid");
  expect(await readSent(page, "ForemanDeal")).toEqual([["pay", 1]]);
});

test("Escape leaves the foreman", async ({ page }) => {
  await page.goto("/foreman/index.html");
  await expect(page.getByRole("listbox", { name: t("for_sale") })).toBeFocused();
  await page.keyboard.press("Escape");
  await expect(page.locator(".foreman")).toHaveCount(0);
  expect(await readSent(page, "ForemanClose")).toEqual([[]]);
});
