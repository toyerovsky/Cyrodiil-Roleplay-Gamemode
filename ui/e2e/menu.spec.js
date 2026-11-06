import { test, expect, readTexts, screenshot, readSent, expectNoHorizontalOverflow, pressInGame } from "./fixtures.js";

const t = readTexts("ui.menu");

const tab = (page, id, words = t) => page.getByRole("tab", { name: words(`tab.${id}`) });

const KEY_E = 69;
const KEY_Q = 81;
const ARROW_DOWN = 40;
const F10 = 121;

test("the menu opens on the character and goes round its tabs from the keyboard and the mouse", async ({ page }, testInfo) => {
  await page.goto("/menu/index.html");
  const menu = page.getByRole("region", { name: "Marcus Varro" });
  await expect(menu).toBeVisible();
  await expect(tab(page, "character")).toBeFocused();
  await expect(tab(page, "character")).toHaveAttribute("aria-selected", "true");
  await expect(page.getByRole("tabpanel")).toContainText("7 h 25 min");
  await expect(page.getByRole("tabpanel")).toContainText("1,240 gold");
  await expectNoHorizontalOverflow(page, ".menu__panel");
  await screenshot(page, testInfo, "menu-character");

  await page.keyboard.press("e");
  await expect(tab(page, "account")).toHaveAttribute("aria-selected", "true");
  await expect(page.getByRole("tabpanel")).toContainText("Toyer");
  await expect(page.getByRole("group", { name: t("account.language") })).toHaveCount(0);
  await screenshot(page, testInfo, "menu-account");

  await page.keyboard.press("ArrowDown");
  await expect(tab(page, "help")).toBeFocused();
  await expect(page.getByRole("heading", { name: t("category.roleplay") })).toBeVisible();
  await screenshot(page, testInfo, "menu-help");

  await page.keyboard.press("q");
  await page.keyboard.press("ArrowUp");
  await expect(tab(page, "character")).toHaveAttribute("aria-selected", "true");

  await tab(page, "keys").click();
  await expect(page.getByRole("tabpanel").locator("kbd")).toHaveText(["G", "H", "Y", "N", "K", "F10", "Home", "End", "Shift+End", "Esc"]);
  await screenshot(page, testInfo, "menu-keys");
});

test("the long help of the staff scrolls, its own heading last", async ({ page }, testInfo) => {
  await page.goto("/menu/index.html#help");
  await expect(tab(page, "help")).toHaveAttribute("aria-selected", "true");
  const headings = page.getByRole("tabpanel").getByRole("heading");
  await expect(headings).toHaveText([t("category.roleplay"), t("category.messages"), t("category.general"), t("category.staff")]);
  await expectNoHorizontalOverflow(page, ".menu__content");
  await screenshot(page, testInfo, "menu-help-staff");

  const content = page.locator(".menu__content");
  const scrollable = await content.evaluate((element) => element.scrollHeight > element.clientHeight);
  expect(scrollable, "the staff's help is longer than the tab").toBe(true);
  await page.keyboard.press("PageDown");
  await expect.poll(() => content.evaluate((element) => element.scrollTop)).toBeGreaterThan(0);
  await content.evaluate((element) => element.scrollTo({ top: element.scrollHeight }));
  await expect(page.locator(".menu__command").last()).toBeInViewport();
  await screenshot(page, testInfo, "menu-help-staff-end");
});

test("Q, E and the arrows switch tabs as the game sends them, and F10 is left to the game", async ({ page }) => {
  await page.goto("/menu/index.html");
  await expect(tab(page, "character")).toBeFocused();
  await page.evaluate(() => window.addEventListener("keydown", (event) => (window.__codes ??= []).push(event.code)));

  await pressInGame(page, KEY_E);
  await expect(tab(page, "account")).toHaveAttribute("aria-selected", "true");
  await page.evaluate(() => document.activeElement.blur());
  await pressInGame(page, ARROW_DOWN);
  await expect(tab(page, "help")).toHaveAttribute("aria-selected", "true");
  await pressInGame(page, KEY_Q);
  await expect(tab(page, "account")).toHaveAttribute("aria-selected", "true");
  expect(await page.evaluate(() => window.__codes), "the keys came without a code, as in the game").toEqual(["", "", ""]);

  await pressInGame(page, F10);
  await expect(page.locator(".menu")).toBeVisible();
  expect(await readSent(page, "MenuClose")).toEqual([]);
});

test("a sanction shows with its end, and a second language can be picked from the keyboard", async ({ page }, testInfo) => {
  await page.goto("/menu/index.html#account");
  await expect(tab(page, "account")).toHaveAttribute("aria-selected", "true");
  await expect(page.locator(".menu__sanction")).toContainText("Shouting over a wedding");
  const languages = page.getByRole("group", { name: t("account.language") });
  await expect(languages.getByRole("button", { name: "English", pressed: true })).toBeVisible();
  await expect(languages.getByRole("button", { name: "Polski", pressed: false })).toBeVisible();
  await expect(page.getByRole("radio")).toHaveCount(0);
  await screenshot(page, testInfo, "menu-sanction");

  await languages.getByRole("button", { name: "English" }).focus();
  await page.keyboard.press("Tab");
  await expect(languages.getByRole("button", { name: "Polski" })).toBeFocused();
  await screenshot(page, testInfo, "menu-language-focused");
  await page.keyboard.press("Enter");
  const polish = readTexts("ui.menu", "pl");
  const polishLanguages = page.getByRole("group", { name: polish("account.language") });
  await expect(polishLanguages.getByRole("button", { name: "Polski", pressed: true })).toBeVisible();
  expect(await readSent(page, "MenuLanguage")).toEqual([["pl"]]);
  await expect(tab(page, "account", polish)).toHaveAttribute("aria-selected", "true");
  await expect(page.getByRole("button", { name: polish("close") })).toBeVisible();
  await expect(page.locator("html")).toHaveAttribute("lang", "pl");
  await screenshot(page, testInfo, "menu-language");
});

test("the dice tab picks a set with the mouse and opens the dice board", async ({ page }, testInfo) => {
  await page.goto("/menu/index.html#dice");
  await expect(tab(page, "dice")).toHaveAttribute("aria-selected", "true");
  const dice = readTexts("ui.dice");
  await expect(page.getByRole("button", { name: dice("set.imperial") })).toHaveAttribute("aria-pressed", "true");
  await expect(page.locator(".menu__roll")).toHaveCount(3);
  await expectNoHorizontalOverflow(page, ".menu__content");
  await screenshot(page, testInfo, "menu-dice");

  await page.getByRole("button", { name: dice("set.dunmer") }).click();
  await expect(page.getByRole("button", { name: dice("set.dunmer") })).toHaveAttribute("aria-pressed", "true");
  await expect(page.getByText(dice("set_text.dunmer"))).toBeVisible();
  expect(await readSent(page, "MenuDiceSet")).toEqual([["dunmer"]]);

  await page.getByRole("button", { name: t("dice.open") }).click();
  expect(await readSent(page, "MenuDice")).toEqual([[]]);
  await expect(page.locator(".menu")).toBeHidden();
});

test("Escape closes the menu", async ({ page }) => {
  await page.goto("/menu/index.html");
  await expect(tab(page, "character")).toBeFocused();
  await page.keyboard.press("Escape");
  await expect(page.locator(".menu")).toHaveCount(0);
  expect(await readSent(page, "MenuClose")).toEqual([[]]);
});

test("the close button closes it too", async ({ page }) => {
  await page.goto("/menu/index.html");
  await page.getByRole("button", { name: t("close") }).click();
  await expect(page.locator(".menu")).toHaveCount(0);
});

test("the lore tab shows how strangers see the character, makes the name public and opens the card", async ({ page }, testInfo) => {
  await page.goto("/menu/index.html#lore");
  await expect(tab(page, "lore")).toHaveAttribute("aria-selected", "true");
  const lore = readTexts("ui.lore");
  const content = page.getByRole("tabpanel");
  await expect(content.locator(".menu__lore-header")).toHaveText("Imperial");
  await expect(content.locator(".menu__lore-person")).toHaveText(["[3]Lucia Gallus", "[12]Dravyn Sadri"]);
  await expect(content.locator(".lore-view__field")).toHaveCount(4);
  await expectNoHorizontalOverflow(page, ".menu__content");
  await screenshot(page, testInfo, "menu-lore");

  await content.getByRole("button", { name: lore("vis.public") }).click();
  await expect(content.getByRole("button", { name: lore("vis.public") })).toHaveAttribute("aria-pressed", "true");
  await expect(content.locator(".menu__lore-header")).toHaveText("Marcus Varro");
  expect(await readSent(page, "MenuLoreName")).toEqual([[true]]);
  await screenshot(page, testInfo, "menu-lore-public");

  await content.getByRole("button", { name: t("lore.open") }).click();
  expect(await readSent(page, "MenuLore")).toEqual([[]]);
  await expect(page.locator(".menu")).toBeHidden();
});

test("the Sanctuary tab says the player is an aggressor for a while yet, and gives the rules", async ({ page }, testInfo) => {
  await page.goto("/menu/index.html#sanctuary");
  await expect(tab(page, "sanctuary")).toHaveAttribute("aria-selected", "true");
  const content = page.getByRole("tabpanel");
  await expect(content.getByRole("status")).toContainText(t("sanctuary.aggressor"));
  await expect(content.locator(".menu__sanctuary-left")).toContainText("4:0");
  await expect(content.locator(".menu__sanctuary-rules li")).toHaveCount(6);
  await expect(content.locator(".menu__sanctuary-rules")).toContainText("40 gold");
  await expectNoHorizontalOverflow(page, ".menu__panel");
  await screenshot(page, testInfo, "menu-sanctuary");
});
