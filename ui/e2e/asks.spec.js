import { test, expect, readTexts, screenshot } from "./fixtures.js";

const t = readTexts("ui.asks");

test("an invitation shows at the top with Y and N, counts down, and goes once answered", async ({ page }, testInfo) => {
  await page.clock.install();
  await page.goto("/asks/index.html");
  const asks = page.getByRole("region", { name: t("label") });
  const invitation = asks.getByRole("article");
  await expect(invitation).toHaveCount(1);
  await expect(invitation).toContainText("Lucia Gallus invites you to their party.");
  await expect(invitation.locator("kbd")).toHaveText(["Y", "N"]);
  await expect(invitation).toContainText("27 s");
  expect(await page.evaluate(() => getComputedStyle(document.body).pointerEvents)).toBe("none");
  await screenshot(page, testInfo, "asks-party");

  await page.clock.runFor(5000);
  await expect(invitation).toContainText("22 s");

  await page.keyboard.press("y");
  await page.clock.runFor(100);
  await expect(asks).toBeHidden();
});

test("a trade waits under the invitation, and the player's own ask shows without keys", async ({ page }, testInfo) => {
  await page.goto("/asks/index.html#trade");
  const cards = page.getByRole("region", { name: t("label") }).getByRole("article");
  await expect(cards).toHaveCount(3);
  await expect(cards.nth(0).locator("kbd")).toHaveCount(2);
  await expect(cards.nth(1)).toContainText(t("next"));
  await expect(cards.nth(1).locator("kbd")).toHaveCount(0);
  await expect(cards.nth(2)).toContainText("Waiting for Mazoga gro-Malog to answer your invitation.");
  await screenshot(page, testInfo, "asks-trade");

  await page.keyboard.press("n");
  await expect(cards).toHaveCount(2);
  await expect(cards.nth(0).locator("kbd")).toHaveCount(2);
  await expect(cards.nth(0)).toContainText("Ra'jirr wants to trade with you.");
});

test("a long name of the one asked wraps inside the card", async ({ page }, testInfo) => {
  await page.goto("/asks/index.html#outgoing");
  const card = page.getByRole("region", { name: t("label") }).getByRole("article");
  await expect(card).toContainText("Aurelianus Valerius-Maximinus");
  const overflows = await card.evaluate((element) => element.scrollWidth > element.clientWidth + 1);
  expect(overflows).toBe(false);
  await screenshot(page, testInfo, "asks-outgoing");
});
