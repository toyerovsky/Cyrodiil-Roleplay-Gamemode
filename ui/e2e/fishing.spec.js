import { test, expect, readTexts, screenshot, readSent, expectNoHorizontalOverflow } from "./fixtures.js";

const t = readTexts("ui.fishing");
const CARD_SHOWN_MS = 3600;

async function openLine(page, scenario) {
  await page.goto(`/fishing/index.html#${scenario}`);
  await expect.poll(() => readSent(page, "FishingReady")).toHaveLength(1);
  await expect(page.locator(".fishing__plate")).toBeVisible();
}

test("with the line in the angler is asked for a spot, the key named and the cast's spot marked", async ({ page }, testInfo) => {
  await openLine(page, "");
  await expect(page.getByRole("status").first()).toHaveText(t("aim"));
  await expect(page.locator(".fishing__hint")).toContainText(t("aim_hint"));
  await expect(page.locator(".fishing__hint kbd")).toHaveText(t("key_space"));
  await expect(page.locator(".fishing__stop")).toContainText(t("stop_hint"));
  await expect(page.locator(".fishing__mark--aim .fishing__ring")).toBeVisible();
  await expectNoHorizontalOverflow(page, ".fishing__plate");
  await screenshot(page, testInfo, "fishing-aim");
});

test("a nibble is said a moment, then the angler waits again", async ({ page }) => {
  await openLine(page, "nibble");
  await expect(page.locator(".fishing__title")).toHaveText(t("nibble"));
  await expect(page.locator(".fishing__title")).toHaveText(t("waiting"));
  await expect(page.locator(".fishing__hint kbd")).toHaveCount(0);
});

test("at the bite a ring closes over the float and the key strikes", async ({ page }, testInfo) => {
  await openLine(page, "bite");
  await expect(page.locator(".fishing__title")).toHaveText(t("bite"));
  await expect(page.locator(".fishing__mark--bite .fishing__ring")).toBeVisible();
  await expect(page.locator(".fishing__hint")).toContainText(t("bite_hint"));
  await screenshot(page, testInfo, "fishing-bite");
});

test("while landing the rod's bend shows against the safe band, and a bend too far is called out", async ({ page }, testInfo) => {
  await openLine(page, "landing");
  const meter = page.getByRole("meter", { name: t("bend") });
  await expect(meter).toHaveAttribute("aria-valuenow", "0.56");
  await expect(page.locator(".fishing__title")).toHaveText(t("landing"));
  await expect(page.locator(".fishing__hint")).toContainText(t("landing_hint"));
  await screenshot(page, testInfo, "fishing-landing");

  await page.goto("/fishing/index.html?shot=tight#tight");
  await expect(page.locator(".fishing__title")).toHaveText(t("tight"));
  await expect(page.locator(".fishing__plate--tight")).toBeVisible();
  await screenshot(page, testInfo, "fishing-tight");
});

test("a catch shows on its card with its size and the trophy, and the card goes after a while", async ({ page }, testInfo) => {
  await page.clock.install();
  await openLine(page, "caught");
  const card = page.locator("[data-card]");
  await expect(card.locator(".fishing__card-name")).toHaveText("Pike");
  await expect(card.locator(".fishing__card-size")).toContainText("84 cm");
  await expect(card.locator(".fishing__trophy")).toHaveText(t("trophy"));
  await expectNoHorizontalOverflow(page, ".fishing__card");
  await screenshot(page, testInfo, "fishing-caught");

  await page.clock.runFor(CARD_SHOWN_MS + 500);
  await expect(card).toHaveCount(0);
});

test("a find shows without a size, and a lost fish is named with no card", async ({ page }, testInfo) => {
  await openLine(page, "found");
  await expect(page.locator(".fishing__card-kind")).toHaveText(t("found"));
  await expect(page.locator(".fishing__card-name")).toHaveText("Flawless Pearl");
  await expect(page.locator(".fishing__card-size")).toHaveCount(0);
  await screenshot(page, testInfo, "fishing-found");

  await page.goto("/fishing/index.html?shot=lost#lost");
  await expect(page.locator(".fishing__title")).toHaveText(t("lost_snapped"));
  await expect(page.locator("[data-card]")).toHaveCount(0);
  await screenshot(page, testInfo, "fishing-lost");
});

test("the page never takes a click from the game", async ({ page }) => {
  await openLine(page, "landing");
  const takesClicks = await page.evaluate(() =>
    [document.body, ".fishing", ".fishing__plate", ".fishing__meter", ".fishing__mark"]
      .map((part) => (typeof part === "string" ? document.querySelector(part) : part))
      .some((element) => getComputedStyle(element).pointerEvents !== "none"),
  );
  expect(takesClicks).toBe(false);
});
