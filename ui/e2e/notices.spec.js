import { test, expect, readTexts, screenshot, readSent, pauseClock, expectNoHorizontalOverflow } from "./fixtures.js";

const t = readTexts("ui.notices");
const server = readTexts("notices");

test("a player reads the board, pins a notice of two lines and takes their own down", async ({ page }, testInfo) => {
  await page.goto("/notices/index.html");
  const window = page.getByRole("region", { name: t("title") });
  const field = page.getByLabel(t("write"));
  const status = page.getByRole("status");
  const notices = page.getByRole("list", { name: t("list") }).getByRole("listitem");
  await expect(window).toBeVisible();
  await expect(field).toBeFocused();
  await expect(notices).toHaveCount(3);
  await expect(notices.nth(0)).toContainText("Lost: a silver ring");
  await expect(notices.nth(1)).toContainText(t("yours"));
  await expect(page.locator("[data-rule]")).toHaveText("Your notices up: 1 of 3.");
  await expectNoHorizontalOverflow(page, ".notices__panel");
  await screenshot(page, testInfo, "notices-open");

  await field.fill("Selling a horse, sound and quiet.");
  await field.press("Enter");
  await field.pressSequentially("Ask at the stables of Weye.");
  await field.press("Control+Enter");
  await expect(status).toHaveText(server("posted"));
  await expect(notices).toHaveCount(4);
  await expect(notices.nth(0).locator(".notice__text")).toHaveText("Selling a horse, sound and quiet.\nAsk at the stables of Weye.");
  await expect(notices.nth(0)).toContainText(t("yours"));
  await expect(field).toHaveValue("");
  await expect(page.locator("[data-rule]")).toHaveText("You can pin another notice in 5 minutes.");
  await expect(page.getByRole("button", { name: t("post") })).toBeDisabled();
  await screenshot(page, testInfo, "notices-posted");
  expect(await readSent(page, "BoardPost")).toEqual([["Selling a horse, sound and quiet.\nAsk at the stables of Weye."]]);

  await notices.nth(0).getByRole("button", { name: t("take_down") }).click();
  await expect(status).toHaveText(server("taken_down"));
  await expect(notices).toHaveCount(3);
  expect((await readSent(page, "BoardTakeDown")).length).toBe(1);
});

test("a notice of nothing but spaces is marked at its field and not pinned", async ({ page }, testInfo) => {
  await page.goto("/notices/index.html");
  const field = page.getByLabel(t("write"));
  await field.fill("     ");
  await page.getByRole("button", { name: t("post") }).click();
  await expect(page.getByRole("alert")).toHaveText("Write a little more: a notice holds 1 to 300 letters.");
  await expect(field).toHaveAttribute("aria-invalid", "true");
  await expect(field).toBeFocused();
  await screenshot(page, testInfo, "notices-refused");
  expect(await readSent(page, "BoardPost")).toEqual([]);

  await field.fill("Now with words");
  await expect(page.getByRole("alert")).toHaveCount(0);
});

test("the window waits for the board and takes no second notice meanwhile", async ({ page }, testInfo) => {
  await page.clock.install();
  await page.goto("/notices/index.html");
  await page.clock.runFor(100);
  await expect(page.getByLabel(t("write"))).toBeVisible();
  await pauseClock(page);

  await page.getByLabel(t("write")).fill("A room to let");
  await page.getByRole("button", { name: t("post") }).click();
  await expect(page.getByRole("status")).toHaveText(t("posting"));
  await expect(page.getByRole("button", { name: t("post") })).toBeDisabled();
  await screenshot(page, testInfo, "notices-posting");

  await page.clock.runFor(200);
  await expect(page.getByRole("status")).toHaveText(server("posted"));
  expect(await readSent(page, "BoardPost")).toEqual([["A room to let"]]);
});

test("a full board of long notices scrolls inside its frame and warns that the oldest goes", async ({ page }, testInfo) => {
  await page.goto("/notices/index.html#full");
  const list = page.getByRole("list", { name: t("list") });
  await expect(list.getByRole("listitem")).toHaveCount(10);
  await expect(page.locator("[data-rule]")).toContainText(t("full"));
  await expectNoHorizontalOverflow(page, ".notices__panel");
  await expectNoHorizontalOverflow(page, ".notices__list");
  await expect(page.getByLabel(t("write"))).toBeInViewport();
  await expect(page.getByRole("button", { name: t("close") })).toBeInViewport();
  await screenshot(page, testInfo, "notices-full");

  const scroll = await list.evaluate((element) => ({ scrollable: element.scrollHeight > element.clientHeight, before: element.scrollTop }));
  expect(scroll.scrollable).toBe(true);
  await list.evaluate((element) => element.scrollTo(0, element.scrollHeight));
  expect(await list.evaluate((element) => element.scrollTop)).toBeGreaterThan(scroll.before);
  await expect(list.getByRole("listitem").last()).toBeInViewport();
  await screenshot(page, testInfo, "notices-full-scrolled");
});

test("a player with as many notices up as one may is told why the board takes no more", async ({ page }, testInfo) => {
  await page.goto("/notices/index.html#limit");
  await expect(page.locator("[data-rule]")).toHaveText(t("too_many"));
  await expect(page.getByRole("button", { name: t("post") })).toBeDisabled();
  await expect(page.getByRole("button", { name: t("take_down") })).toHaveCount(3);
  await screenshot(page, testInfo, "notices-limit");
});

test("a player who just pinned a notice sees the wait run down to the next", async ({ page }, testInfo) => {
  await page.clock.install();
  await page.goto("/notices/index.html#wait");
  await page.clock.runFor(100);
  await expect(page.locator("[data-rule]")).toHaveText("You can pin another notice in 4 minutes.");
  await expect(page.getByRole("button", { name: t("post") })).toBeDisabled();
  await screenshot(page, testInfo, "notices-wait");

  await page.clock.runFor(240_000);
  await expect(page.locator("[data-rule]")).toHaveText("Your notices up: 1 of 3.");
  await expect(page.getByRole("button", { name: t("post") })).toBeEnabled();
});

test("an empty board says so", async ({ page }, testInfo) => {
  await page.goto("/notices/index.html#empty");
  await expect(page.getByText(t("empty"))).toBeVisible();
  await screenshot(page, testInfo, "notices-empty");
});

test("Escape leaves the board, from the notice's field too", async ({ page }) => {
  await page.goto("/notices/index.html");
  await expect(page.getByLabel(t("write"))).toBeFocused();
  await page.keyboard.press("Escape");
  await expect(page.locator(".notices")).toHaveCount(0);
  expect(await readSent(page, "BoardClose")).toEqual([[]]);
});
