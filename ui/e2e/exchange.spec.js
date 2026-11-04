import { test, expect, readTexts, screenshot, readSent, expectNoHorizontalOverflow } from "./fixtures.js";

const t = readTexts("ui.exchange");
const CONFIRM_DELAY_MS = 3000;
const OFFER_AFTER_MS = 400;
const LUCIA_CONFIRMS_MS = 600;
const DONE_SHOWS_MS = 2000;

test("both sides see the offer, a change waits again, and the trade is done once both confirm", async ({ page }, testInfo) => {
  await page.clock.install();
  await page.goto("/exchange/index.html");

  const window = page.getByRole("region", { name: "Trade with Lucia Gallus" });
  const gold = page.getByRole("textbox", { name: t("gold") });
  await expect(window).toBeVisible();
  await expect(gold).toBeFocused();
  await expect(page.getByRole("button", { name: "Confirm in 3 s" })).toBeDisabled();
  await expectNoHorizontalOverflow(page, ".exchange__panel");
  await screenshot(page, testInfo, "exchange-open");

  await gold.fill("50");
  await page.clock.runFor(OFFER_AFTER_MS + 100);
  await expect(page.getByRole("button", { name: "Confirm in 3 s" })).toBeDisabled();
  await page.clock.runFor(CONFIRM_DELAY_MS);
  const confirm = page.getByRole("button", { name: t("confirm"), exact: true });
  await expect(confirm).toBeEnabled();
  await screenshot(page, testInfo, "exchange-offered");

  await confirm.click();
  await expect(page.getByRole("button", { name: "Waiting for Lucia Gallus" })).toBeDisabled();
  await page.clock.runFor(LUCIA_CONFIRMS_MS);
  await expect(page.getByRole("status")).toHaveText("Trade done: gave 50 gold coins");
  await expect(page.getByRole("region", { name: "Lucia Gallus offers" })).toContainText(t("confirmed"));
  await expect(page.getByRole("button", { name: t("done") })).toBeDisabled();
  await screenshot(page, testInfo, "exchange-done");

  await page.clock.runFor(DONE_SHOWS_MS);
  await expect(window).toBeHidden();
  expect(await readSent(page, "ExchangeOffer")).toEqual([[50]]);
  expect(await readSent(page, "ExchangeConfirm")).toEqual([[2]]);
});

test("Lucia's offer shows with her confirmation, and more than one carries is refused", async ({ page }, testInfo) => {
  await page.clock.install();
  await page.goto("/exchange/index.html#offered");
  const theirs = page.getByRole("region", { name: "Lucia Gallus offers" });
  await expect(theirs).toContainText("40");
  await expect(theirs).toContainText(t("confirmed"));

  await page.getByRole("textbox", { name: t("gold") }).fill("500");
  await page.getByRole("textbox", { name: t("gold") }).press("Enter");
  await expect(page.getByRole("status")).toHaveText("Offer from 0 to 120 gold.");
  await screenshot(page, testInfo, "exchange-refused");
});

test("items go into the offer, the other side's show, and the trade is done with both", async ({ page }, testInfo) => {
  await page.clock.install();
  await page.goto("/exchange/index.html#offered");
  const yours = page.getByRole("region", { name: t("yours") });
  const theirs = page.getByRole("region", { name: "Lucia Gallus offers" });
  const list = page.getByRole("region", { name: t("your_items") });
  await expect(theirs).toContainText("Steel Longsword (used)");
  await expect(theirs).toContainText("Resist Normal Weapons Quilted Doublet");
  await expectNoHorizontalOverflow(page, ".exchange__panel");
  await screenshot(page, testInfo, "exchange-theirs");

  await list.getByRole("button", { name: "Add Iron Arrow to your offer" }).click();
  await list.getByRole("button", { name: "Add Cursed Gauntlets of Resilient Flesh to your offer" }).click();
  await list.getByRole("button", { name: "Add Iron Longsword (used) to your offer" }).click();
  await page.getByRole("textbox", { name: "How many of Iron Arrow" }).fill("25");
  await page.clock.runFor(OFFER_AFTER_MS + 100);
  await expect(list.getByRole("button", { name: "Add Iron Arrow to your offer" })).toContainText("×35");
  await expect(list.getByRole("button", { name: "Add Iron Longsword (used) to your offer" })).toBeDisabled();
  await expect(yours).toContainText("Cursed Gauntlets of Resilient Flesh");
  await expectNoHorizontalOverflow(page, ".exchange__panel");
  await screenshot(page, testInfo, "exchange-items");

  await page.clock.runFor(CONFIRM_DELAY_MS);
  await page.getByRole("button", { name: t("confirm"), exact: true }).click();
  await page.clock.runFor(LUCIA_CONFIRMS_MS);
  await expect(page.getByRole("status")).toHaveText("Trade done: received 40 gold coins, received 5 items, gave 27 items");
  await screenshot(page, testInfo, "exchange-items-done");

  expect(await readSent(page, "ExchangeOfferItem")).toEqual([
    ["Iron Arrow", 25],
    ["Cursed Gauntlets of Resilient Flesh", 1],
    ["Iron Longsword, used", 1],
  ]);
});

test("a long list scrolls inside the window and the filter narrows it", async ({ page }, testInfo) => {
  await page.goto("/exchange/index.html");
  const list = page.getByRole("region", { name: t("your_items") }).getByRole("list");
  await expect(list).toBeVisible();

  const scrolled = await list.evaluate((element) => {
    element.scrollTop = element.scrollHeight;
    return element.scrollTop;
  });
  expect(scrolled).toBeGreaterThan(0);
  await expect(page.getByRole("button", { name: "Add Yarn to your offer" })).toBeInViewport();
  await screenshot(page, testInfo, "exchange-scrolled");

  await page.getByRole("textbox", { name: t("filter") }).fill("sword");
  await expect(page.getByRole("button", { name: /to your offer/ })).toHaveCount(2);
  await page.getByRole("textbox", { name: t("filter") }).fill("mace");
  await expect(page.getByText(t("no_match"))).toBeVisible();
  await screenshot(page, testInfo, "exchange-no-match");
});

test("a player with nothing to trade reads why", async ({ page }, testInfo) => {
  await page.goto("/exchange/index.html#bare");

  await expect(page.getByText(t("empty"))).toBeVisible();
  await expect(page.getByText(t("not_listed"))).toBeVisible();
  await screenshot(page, testInfo, "exchange-bare");
});

test("a count above what the player carries comes back with the server's refusal", async ({ page }, testInfo) => {
  await page.goto("/exchange/index.html");
  await page.getByRole("button", { name: "Add Bread Loaf to your offer" }).click();
  const count = page.getByRole("textbox", { name: "How many of Bread Loaf" });
  await count.fill("9");
  await count.press("Enter");

  await expect(page.getByRole("status")).toHaveText("Offer from 0 to 5 of Bread Loaf.");
  await expect(page.getByRole("button", { name: "Add Bread Loaf to your offer" })).toContainText("×5");
  await screenshot(page, testInfo, "exchange-item-refused");
});

test("Escape calls the trade off and closes the window", async ({ page }) => {
  await page.goto("/exchange/index.html");
  await expect(page.getByRole("textbox", { name: t("gold") })).toBeFocused();

  await page.keyboard.press("Escape");
  await expect(page.getByRole("region", { name: "Trade with Lucia Gallus" })).toBeHidden();
  expect(await readSent(page, "ExchangeCancel")).toHaveLength(1);
});
