import { test, expect, readTexts, screenshot, readSent, pauseClock, expectNoHorizontalOverflow } from "./fixtures.js";

const t = readTexts("ui.stable");
const server = readTexts("stable");
const fill = (text, args) => text.replace(/\{(\w+)\}/g, (_, name) => String(args[name]));
const action = (key, horse) => fill(t("action"), { action: t(key), horse: t(`horse.${horse}`) });

test("a first horse is bought with the keys and the window shows it as the character's own", async ({ page }, testInfo) => {
  await page.goto("/stable/index.html");
  const window = page.getByRole("region", { name: t("title") });
  await expect(window).toBeVisible();
  await expect(page.getByRole("list", { name: t("horses") }).getByRole("listitem")).toHaveCount(5);
  await expect(page.locator('[data-sum="horse"] dd')).toHaveText(t("none"));
  await expect(page.locator('[data-sum="gold"] dd')).toHaveText("1,340 gold");
  await expect(page.getByRole("button", { name: action("buy", "Paint") })).toBeFocused();
  await expect(page.getByRole("button", { name: action("too_poor", "Chestnut") })).toBeDisabled();
  await expect(page.locator(".stable__note")).toHaveCount(0);
  await expectNoHorizontalOverflow(page, ".stable__panel");
  await screenshot(page, testInfo, "stable-open");

  await page.keyboard.press("Tab");
  await expect(page.getByRole("button", { name: action("buy", "Bay") })).toBeFocused();
  await page.keyboard.press("Enter");
  await expect(page.getByRole("status")).toHaveText(fill(server("bought"), { horse: server("horse.bay"), price: 1000 }));
  await expect(page.locator('[data-sum="horse"] dd')).toHaveText(t("horse.Bay"));
  await expect(page.locator('[data-sum="gold"] dd')).toHaveText("340 gold");
  await expect(page.getByRole("button", { name: action("owned", "Bay") })).toBeDisabled();
  await expect(page.getByRole("button", { name: t("close") })).toBeFocused();
  await expect(page.locator(".stable__note")).toHaveText(fill(t("replace_note"), { horse: t("horse.Bay") }));
  await screenshot(page, testInfo, "stable-bought");
  expect(await readSent(page, "StableBuy")).toEqual([["Bay"]]);
});

test("another horse is bought only once the player says the one owned may go", async ({ page }, testInfo) => {
  await page.goto("/stable/index.html#owned");
  await expect(page.locator('[data-sum="horse"] dd')).toHaveText(t("horse.Bay"));
  await expect(page.locator(".stable__note")).toHaveText(fill(t("replace_note"), { horse: t("horse.Bay") }));
  await screenshot(page, testInfo, "stable-owned");

  const black = page.getByRole("button", { name: action("buy", "Black") });
  await black.click();
  const ask = page.getByRole("alertdialog");
  await expect(ask).toContainText(fill(t("confirm"), { horse: t("horse.Black"), price: "5,000", owned: t("horse.Bay") }));
  await expect(ask.getByRole("button", { name: t("confirm_yes") })).toBeFocused();
  await expectNoHorizontalOverflow(page, ".stable__panel");
  const scrolls = await page.locator(".stable__panel").evaluate((panel) => panel.scrollHeight > panel.clientHeight + 1);
  expect(scrolls, "the window with its question fits on the screen").toBe(false);
  await screenshot(page, testInfo, "stable-confirm");

  await page.keyboard.press("Escape");
  await expect(ask).toHaveCount(0);
  await expect(black).toBeFocused();
  await expect(page.locator(".stable")).toBeVisible();
  expect(await readSent(page, "StableBuy")).toEqual([]);

  await black.click();
  await page.getByRole("button", { name: t("confirm_yes") }).click();
  await expect(page.getByRole("status")).toHaveText(
    fill(server("bought_instead"), { horse: server("horse.black"), price: 5000, old: server("horse.bay") }),
  );
  await expect(page.locator('[data-sum="horse"] dd')).toHaveText(t("horse.Black"));
  await expect(page.locator('[data-sum="gold"] dd')).toHaveText("1,200 gold");
  expect(await readSent(page, "StableBuy")).toEqual([["Black"]]);
  expect(await readSent(page, "StableClose")).toEqual([]);
});

test("the window waits for the stablemaster and sells nothing more meanwhile", async ({ page }, testInfo) => {
  await page.clock.install();
  await page.goto("/stable/index.html");
  await page.clock.runFor(100);
  await expect(page.getByRole("button", { name: action("buy", "Paint") })).toBeVisible();
  await pauseClock(page);

  await page.getByRole("button", { name: action("buy", "Paint") }).click();
  await expect(page.getByRole("status")).toHaveText(t("waiting"));
  await expect(page.getByRole("button", { name: action("buy", "Bay") })).toBeDisabled();
  await screenshot(page, testInfo, "stable-waiting");

  await page.clock.runFor(100);
  await expect(page.getByRole("status")).toHaveText(fill(server("bought"), { horse: server("horse.paint"), price: 500 }));
  expect(await readSent(page, "StableBuy")).toEqual([["Paint"]]);
});

test("a character too poor for any horse can only leave", async ({ page }, testInfo) => {
  await page.goto("/stable/index.html#poor");
  await expect(page.locator('[data-sum="gold"] dd')).toHaveText("40 gold");
  for (const horse of ["Paint", "Bay", "Chestnut", "White", "Black"])
    await expect(page.getByRole("button", { name: action("too_poor", horse) })).toBeDisabled();
  await expect(page.getByRole("button", { name: t("close") })).toBeFocused();
  await screenshot(page, testInfo, "stable-poor");
});

test("Escape leaves the stable", async ({ page }) => {
  await page.goto("/stable/index.html");
  await expect(page.getByRole("button", { name: action("buy", "Paint") })).toBeFocused();
  await page.keyboard.press("Escape");
  await expect(page.locator(".stable")).toHaveCount(0);
  expect(await readSent(page, "StableClose")).toEqual([[]]);
});
