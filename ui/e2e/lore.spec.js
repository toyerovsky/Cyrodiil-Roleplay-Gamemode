import { test, expect, readTexts, screenshot, readSent, expectNoHorizontalOverflow, pressInGame } from "./fixtures.js";

const t = readTexts("ui.lore");

const EQUAL = 187;
const DELETE = 46;
const SPACE = 32;
// The dice fly on the dice board's small board this long before the trait's number shows (lore/App.vue), and the
// pretend server answers after a moment.
const ROLL_SHOWN_MS = 8000;

const section = (page, id) => page.getByRole("tab", { name: new RegExp(`^${t(`part.${id}`)}`) });
const row = (page, id) => page.locator(`.lore-row[data-field="${id}"]`);
const detail = (page) => page.locator(".lore__detail");
const opened = (page) => expect(page.locator(".lore__name")).toHaveText("Marcus Varro");
const sentOps = async (page, op) => (await readSent(page, "LoreAsk")).map(([, json]) => JSON.parse(json)).filter((request) => request.op === op);

test("the card opens on its identity, the name private, and goes round the sections by keys and the mouse", async ({ page }, testInfo) => {
  await page.goto("/lore/index.html");
  await opened(page);
  await expect(page.getByRole("listbox", { name: t("part.identity") })).toBeFocused();
  await expect(row(page, "name")).toHaveAttribute("aria-selected", "true");
  await expect(row(page, "name")).toContainText(t("vis.private"));
  await expect(page.locator(".lore__seen")).toHaveText("Strangers see you as: Imperial");
  await expectNoHorizontalOverflow(page, ".lore__panel");
  await screenshot(page, testInfo, "lore-identity");

  await page.keyboard.press("ArrowRight");
  await expect(section(page, "heritage")).toHaveAttribute("aria-selected", "true");
  await page.keyboard.press("e");
  await expect(section(page, "life")).toHaveAttribute("aria-selected", "true");
  await section(page, "story").click();
  await row(page, "notes").click();
  await expect(row(page, "notes")).toContainText(t("vis.owner"));
  await expect(detail(page).getByRole("button", { name: t("vis.public") })).toHaveCount(0);
  await screenshot(page, testInfo, "lore-notes");
});

test("the name turns public with the mouse and private again with Space as the game sends it", async ({ page }, testInfo) => {
  await page.goto("/lore/index.html");
  await opened(page);
  await detail(page).getByRole("button", { name: t("vis.public") }).click();
  await expect(detail(page).getByRole("button", { name: t("vis.public") })).toHaveAttribute("aria-pressed", "true");
  await expect.poll(async () => (await sentOps(page, "namevis")).map((request) => request.vis)).toEqual(["public"]);
  await expect(page.locator(".lore__seen")).toHaveCount(0);
  await screenshot(page, testInfo, "lore-name-public");

  await page.getByRole("listbox").focus();
  await pressInGame(page, SPACE);
  await expect(row(page, "name")).toContainText(t("vis.private"));
  await expect.poll(async () => (await sentOps(page, "namevis")).map((request) => request.vis)).toEqual(["public", "private"]);
});

test("a line is written and saved by keys, a long text by the mouse, and a refusal stays at its box", async ({ page }, testInfo) => {
  await page.goto("/lore/index.html");
  await opened(page);
  await page.keyboard.press("ArrowDown");
  await page.keyboard.press("Enter");
  const box = detail(page).locator(".lore-edit__box");
  await expect(box).toBeFocused();
  await expect(box).toHaveValue("Legion veteran");
  await box.fill("Caravan guard of the Gold Road");
  await screenshot(page, testInfo, "lore-text-edit");
  await page.keyboard.press("Enter");
  await expect(row(page, "title")).toContainText("Caravan guard of the Gold Road");
  await expect(page.locator(".lore__status")).toHaveText("Title set.");

  await section(page, "story").click();
  await row(page, "history").dblclick();
  const long = detail(page).locator("textarea");
  await expect(long).toBeFocused();
  await long.press("End");
  await long.press("Enter");
  await long.pressSequentially("Then the war came.");
  await screenshot(page, testInfo, "lore-long-edit");
  await detail(page).getByRole("button", { name: t("save") }).click();
  await expect(row(page, "history")).toContainText("Then the war came.");

  await section(page, "traits").click();
  await detail(page).getByRole("button", { name: t("trait.type") }).click();
  await detail(page).locator(".lore-edit__box--number").fill("3");
  await page.keyboard.press("Enter");
  await expect(detail(page).getByRole("alert")).toHaveText("Fearful takes a number from 5 to 100.");
  await expect(detail(page).locator(".lore-edit__box--number")).toHaveAttribute("aria-invalid", "true");
  await screenshot(page, testInfo, "lore-trait-refused");
});

test("the homeland is picked from the lore list, its native lands first, by search and keys", async ({ page }, testInfo) => {
  await page.goto("/lore/index.html");
  await opened(page);
  await section(page, "heritage").click();
  await row(page, "homeland").click();
  await page.keyboard.press("Enter");
  const list = detail(page).getByRole("listbox", { name: "Homeland" });
  await expect(list.locator(".lore-list__group").first()).toHaveText("Native lands");
  await expect(list.locator(".lore-option--cursor")).toContainText("Cyrodiil");
  await expect(detail(page).locator(".lore-pick__summary")).toBeVisible();
  await expectNoHorizontalOverflow(page, ".lore__detail");
  await screenshot(page, testInfo, "lore-pick-homeland");

  await page.keyboard.type("sky");
  await expect(list.locator(".lore-option")).toHaveText(["Skyrim"]);
  await page.keyboard.press("Enter");
  await expect(row(page, "homeland")).toContainText("Skyrim");
  expect((await sentOps(page, "pick")).at(-1)).toMatchObject({ field: "homeland", id: "province.skyrim" });

  await row(page, "homeRegion").click();
  await detail(page).getByRole("button", { name: t("pick") }).click();
  await expect(detail(page).locator(".lore-pick__note")).toHaveText(/Nothing in the lore list fits Home region/);
});

test("a trait is rolled with R, the number shows once the dice have landed, and the reroll stays for good", async ({ page }, testInfo) => {
  await page.goto("/lore/index.html");
  await opened(page);
  await section(page, "traits").click();
  await expect(row(page, "trait.lazy")).toContainText(t("trait.one_reroll"));
  await row(page, "trait.lazy").click();
  await expect(detail(page).getByRole("button", { name: t("trait.keep") })).toBeVisible();
  await screenshot(page, testInfo, "lore-trait-rolled");

  await page.keyboard.press("r");
  await expect(detail(page).locator(".lore-trait-big--rolling")).toBeVisible();
  await expect(page.locator(".lore__status")).toHaveText("Rerolling 5d20 for Lazy...");
  await screenshot(page, testInfo, "lore-trait-rolling");
  await expect(detail(page).locator(".lore-detail__fixed")).toHaveText(t("trait.fixed_rolled"), { timeout: ROLL_SHOWN_MS });
  await expect(detail(page).locator(".lore-detail__rolls li")).toHaveCount(2);
  await page.keyboard.press("r");
  await expect(page.locator(".lore__status")).toHaveText("Lazy is set for good: no more rolls.");
  await screenshot(page, testInfo, "lore-trait-fixed");
});

test("the card as a stranger sees it, and back", async ({ page }, testInfo) => {
  await page.goto("/lore/index.html");
  await opened(page);
  await page.getByRole("button", { name: t("preview") }).click();
  await expect(detail(page).getByRole("heading", { name: t("preview.title_public") })).toBeVisible();
  await expect(detail(page).locator(".lore-view__header")).toHaveText("Imperial");
  await expect(detail(page).locator(".lore-view")).not.toContainText("Owes Lucia");
  await screenshot(page, testInfo, "lore-preview");
  await detail(page).getByRole("button", { name: t("preview.as_private") }).click();
  await expect(detail(page).locator(".lore-view__header")).toHaveText("Marcus Varro");
  await expect(detail(page).locator(".lore-view")).toContainText("Cyrodiil");
  await expect(detail(page).locator(".lore-view")).not.toContainText("Owes Lucia");
  await screenshot(page, testInfo, "lore-preview-known");
  await page.keyboard.press("Escape");
  await expect(detail(page).locator(".lore-view")).toHaveCount(0);
  expect(await readSent(page, "LoreClose")).toEqual([]);
});

test("nearby: an introduction accepted with =, a card read and left, a stranger made again on the second Delete", async ({ page }, testInfo) => {
  await page.goto("/lore/index.html");
  await opened(page);
  await section(page, "nearby").click();
  const players = page.getByRole("listbox", { name: t("part.nearby") });
  await expect(players.getByRole("option")).toHaveCount(3);
  await expect(section(page, "nearby").locator(".lore__count")).toHaveText("1");
  await players.locator('[data-player="12"]').click();
  await expect(detail(page).getByRole("button", { name: new RegExp(t("nearby.accept")) })).toBeVisible();
  await screenshot(page, testInfo, "lore-nearby");

  await pressInGame(page, EQUAL);
  await expect(page.locator(".lore__status")).toHaveText("You and Dravyn Sadri know each other now.");
  await expect(players.locator('[data-player="12"]')).toContainText("Dravyn Sadri");

  await players.locator('[data-player="9"]').dblclick();
  await expect(page.locator(".rp-panel__title")).toHaveText("Lucia Gallus");
  await expect(page.locator(".lore__viewer")).toContainText("Left at sixteen to see the Empire.");
  await screenshot(page, testInfo, "lore-viewer-nearby");
  await page.keyboard.press("Escape");
  await expect(players).toBeVisible();

  await players.locator('[data-player="9"]').click();
  await pressInGame(page, DELETE);
  await expect(page.locator(".lore__status")).toHaveText(/click again to become a stranger to Lucia Gallus/);
  await pressInGame(page, DELETE);
  await expect(page.locator(".lore__status")).toHaveText("Lucia Gallus no longer knows you.");
  await expect(players.locator('[data-player="9"]')).toContainText(t("relation.youKnow"));
});

test("another player's card from /lore scrolls by keys and closes on Esc, and End is left to the game", async ({ page }, testInfo) => {
  await page.goto("/lore/index.html#view");
  await expect(page.locator(".rp-panel__title")).toHaveText("Lucia Gallus");
  await expect(page.locator(".lore__viewer")).toBeFocused();
  await expectNoHorizontalOverflow(page, ".lore__panel");
  await screenshot(page, testInfo, "lore-viewer");
  await page.keyboard.press("End");
  await expect(page.locator(".lore")).toBeVisible();
  await page.keyboard.press("Escape");
  expect(await readSent(page, "LoreClose")).toEqual([[]]);
  await expect(page.locator(".lore")).toHaveCount(0);
});

test("a stranger with no card, and a card begun from nothing", async ({ page }, testInfo) => {
  await page.goto("/lore/index.html#nocard");
  await expect(page.locator(".lore-view__none")).toHaveText("Bosmer has no lore card yet.");
  await screenshot(page, testInfo, "lore-nocard");

  await page.goto("/lore/index.html?shot=empty#empty");
  await opened(page);
  await section(page, "heritage").click();
  await expect(detail(page).locator(".lore-detail__game")).toHaveText("In the game your character is: Imperial.");
  await row(page, "subtype").click();
  await expect(detail(page).locator(".lore-detail__game")).toHaveText("Pick this first: Race. It decides what fits here.");
  await screenshot(page, testInfo, "lore-empty");
});

test("in Polish the card keeps its frame, and the races keep their names", async ({ page }, testInfo) => {
  await page.goto("/lore/index.html");
  await opened(page);
  await page.evaluate(() => window.__readym.emit("Locale", "pl"));
  const polish = readTexts("ui.lore", "pl");
  await expect(page.getByRole("heading", { name: polish("title") })).toBeVisible();
  await page.getByRole("tab", { name: new RegExp(`^${polish("part.nearby")}`) }).click();
  await page.locator('[data-player="12"]').click();
  await expect(detail(page).getByRole("heading", { name: "Dunmer" })).toBeVisible();
  await expectNoHorizontalOverflow(page, ".lore__panel");
  await screenshot(page, testInfo, "lore-polish-nearby");
});
