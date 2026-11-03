import { test, expect, readTexts, screenshot, readSent, expectNoHorizontalOverflow, pauseClock } from "./fixtures.js";

const t = readTexts("ui.creator");
// The pretend game's wait before it answers a name.
const ANSWER_MS = 300;

test("a new player names a character, is refused a taken name, and moves on to the appearance", async ({ page }, testInfo) => {
  await page.clock.install();
  await page.goto("/creator/index.html");

  const form = page.getByRole("region", { name: t("title") });
  const first = page.getByRole("textbox", { name: t("first_name") });
  const last = page.getByRole("textbox", { name: t("last_name") });
  await expect(form).toBeVisible();
  await pauseClock(page);
  await expect(first).toBeFocused();
  await expect(page.getByRole("button", { name: t("submit") })).toBeDisabled();
  await expectNoHorizontalOverflow(page, ".creator__panel");
  await screenshot(page, testInfo, "creator-empty");

  await first.fill("Marius");
  await last.fill("Varro");
  await expect(page.getByRole("button", { name: t("submit") })).toBeEnabled();
  await screenshot(page, testInfo, "creator-written");
  await page.getByRole("button", { name: t("submit") }).click();
  await expect(page.getByRole("button", { name: t("checking") })).toBeDisabled();
  await page.clock.runFor(ANSWER_MS);
  await expect(page.getByRole("alert")).toHaveText("Someone in Cyrodiil already goes by Marius Varro.");
  await expect(first).toBeFocused();
  await expect(first).toHaveAttribute("aria-invalid", "true");
  await screenshot(page, testInfo, "creator-taken");

  await last.fill("Gallus");
  await last.press("Enter");
  await expect(page.getByRole("button", { name: t("checking") })).toBeDisabled();
  await page.clock.runFor(ANSWER_MS);
  await expect(form).toBeHidden();

  expect(await readSent(page, "CreatorReady")).toHaveLength(1);
  expect(await readSent(page, "SubmitName")).toEqual([["Marius", "Varro"], ["Marius", "Gallus"]]);
});

test("Enter in the first name moves on to the last name instead of sending half a name", async ({ page }) => {
  await page.goto("/creator/index.html");
  const first = page.getByRole("textbox", { name: t("first_name") });
  await first.fill("Ra'jirr");
  await first.press("Enter");
  await expect(page.getByRole("textbox", { name: t("last_name") })).toBeFocused();
  expect(await readSent(page, "SubmitName")).toEqual([]);
});

test("the form stays off screen until the game shows it", async ({ page }) => {
  await page.goto("/creator/index.html#waiting");
  await expect.poll(() => readSent(page, "CreatorReady")).toHaveLength(1);
  const form = page.getByRole("region", { name: t("title") });
  await expect(form).toBeHidden();

  await page.evaluate(() => window.__readym.emit("CreatorShown", true));
  await expect(form).toBeVisible();
  await expect(page.getByRole("textbox", { name: t("first_name") })).toBeFocused();
});
