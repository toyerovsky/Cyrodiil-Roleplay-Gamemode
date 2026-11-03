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

test("Escape calls the trade off and closes the window", async ({ page }) => {
  await page.goto("/exchange/index.html");
  await expect(page.getByRole("textbox", { name: t("gold") })).toBeFocused();

  await page.keyboard.press("Escape");
  await expect(page.getByRole("region", { name: "Trade with Lucia Gallus" })).toBeHidden();
  expect(await readSent(page, "ExchangeCancel")).toHaveLength(1);
});
