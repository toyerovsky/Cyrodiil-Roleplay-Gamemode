import { test, expect, readTexts, screenshot, readSent, expectNoHorizontalOverflow } from "./fixtures.js";

const t = readTexts("ui.admin");

const tab = (page, id) => page.getByRole("tab", { name: t(`tab.${id}`) });

test("the panel opens on the players, a card runs its buttons and asks for a reason first", async ({ page }, testInfo) => {
  await page.goto("/admin/index.html");
  await expect(page.getByRole("region", { name: t("title") })).toBeVisible();
  await expect(tab(page, "players")).toBeFocused();
  await expect(page.locator(".admin__duty-state")).toHaveText(t("duty.on"));
  await expectNoHorizontalOverflow(page, ".admin__panel");
  await screenshot(page, testInfo, "admin-players");

  await page.locator('[data-player="4"]').click();
  const card = page.getByRole("region", { name: /\[4\] Brannoc/ });
  await expect(card).toContainText("3f2b9d4e-8a1c-4c6e-9b7a-1d2e3f4a5b6c");
  await expect(card.getByRole("button", { name: t("action.unfreeze") })).toBeVisible();
  await expect(card.getByRole("button", { name: t("action.freeze"), exact: true })).toHaveCount(0);
  await expectNoHorizontalOverflow(page, ".admin-card");
  await screenshot(page, testInfo, "admin-card");

  await card.getByRole("button", { name: t("action.warn") }).click();
  const dialog = page.getByRole("dialog");
  await expect(dialog.getByLabel(t("field.reason"))).toBeFocused();
  await dialog.getByRole("button", { name: t("dialog.run") }).click();
  await expect(dialog.getByRole("alert")).toBeVisible();
  await screenshot(page, testInfo, "admin-dialog-refused");
  await dialog.getByLabel(t("field.reason")).fill("Running from the guards mid scene");
  await page.keyboard.press("Enter");
  await expect(page.getByRole("dialog")).toHaveCount(0);
  expect((await readSent(page, "AdminRun")).at(-1)).toEqual(["/awarn 4 Running from the guards mid scene"]);
  await expect(page.locator(".admin__status")).toContainText("Warned Brannoc");

  await card.getByRole("button", { name: t("action.heal") }).click();
  expect((await readSent(page, "AdminRun")).at(-1)).toEqual(["/aheal 4"]);

  await card.getByLabel(t("card.item_search")).fill("lock");
  await card.locator(".admin-items--found").getByRole("button", { name: t("action.give") }).click();
  await page.getByRole("dialog").getByLabel(t("field.count")).fill("5");
  await page.keyboard.press("Enter");
  expect((await readSent(page, "AdminRun")).at(-1)).toEqual(["/aitem give 4 Lockpick 5"]);
  await screenshot(page, testInfo, "admin-card-items");

  await page.keyboard.press("Escape");
  expect(await readSent(page, "AdminClose")).toHaveLength(1);
});

test("the tabs and the filters of the players answer the keyboard", async ({ page }, testInfo) => {
  await page.goto("/admin/index.html");
  await expect(tab(page, "players")).toBeFocused();
  await expect(page.getByRole("radio")).toHaveCount(0);

  const filters = page.getByRole("group", { name: t("players.filter") });
  await expect(filters.getByRole("button", { name: t("filter.all"), pressed: true })).toBeVisible();
  await filters.getByRole("button", { name: t("filter.all") }).focus();
  await page.keyboard.press("Tab");
  await page.keyboard.press("Tab");
  await expect(filters.getByRole("button", { name: t("filter.muted") })).toBeFocused();
  await page.keyboard.press("Space");
  await expect(filters.getByRole("button", { name: t("filter.muted"), pressed: true })).toBeVisible();
  await expect(filters.getByRole("button", { pressed: true })).toHaveCount(1);
  await expect(page.locator("[data-player]")).toHaveCount(1);
  await screenshot(page, testInfo, "admin-filter-muted");

  await page.keyboard.press("e");
  await expect(tab(page, "players")).toHaveAttribute("aria-selected", "true");
  await tab(page, "players").focus();
  await page.keyboard.press("e");
  await expect(tab(page, "items")).toBeFocused();
  await expect(tab(page, "items")).toHaveAttribute("aria-selected", "true");
  await page.keyboard.press("ArrowDown");
  await expect(tab(page, "sanctions")).toBeFocused();
  await page.keyboard.press("q");
  await page.keyboard.press("q");
  await expect(tab(page, "players")).toHaveAttribute("aria-selected", "true");
  await expect(page.getByRole("tabpanel", { name: t("tab.players") })).toBeVisible();
});

test("off duty the panel shows and waits for the duty", async ({ page }, testInfo) => {
  await page.goto("/admin/index.html#offduty");
  await expect(page.locator(".admin__needed")).toHaveText(t("duty.needed"));
  await page.locator('[data-player="3"]').click();
  await expect(page.locator('[data-action="heal"]')).toBeDisabled();
  await expect(page.locator('[data-action="ban"]')).toHaveCount(0);
  const card = page.getByRole("region", { name: /\[3\] Lucia/ });
  await expect(card.getByRole("heading", { name: t("card.history") })).toBeVisible();
  await expect(card.getByText(t("loading"))).toHaveCount(0);
  await screenshot(page, testInfo, "admin-off-duty");

  await page.getByRole("button", { name: t("duty.start") }).click();
  await expect(page.locator(".admin__duty-state")).toHaveText(t("duty.on"));
  await expect(page.locator('[data-action="heal"]')).toBeEnabled();
  expect(await readSent(page, "AdminRun")).toEqual([["/duty"]]);
});

test("the card the server names opens with the panel, a long name and a dungeon included", async ({ page }, testInfo) => {
  await page.goto("/admin/index.html#card");
  const card = page.getByRole("region", { name: /\[4\]/ });
  await expect(card).toContainText("L_PersistentDungeon-1A2B3");
  await expectNoHorizontalOverflow(page, ".admin-card");
  await card.evaluate((element) => element.scrollTo(0, element.scrollHeight));
  await expect(card.getByRole("heading", { name: t("card.history") })).toBeInViewport();
  await screenshot(page, testInfo, "admin-card-history");
});

test("the sanctions in force, each with its lift", async ({ page }, testInfo) => {
  await page.goto("/admin/index.html#sanctions");
  await expect(page.getByRole("table")).toContainText("griefer42");
  await expectNoHorizontalOverflow(page, ".admin__panel");
  await screenshot(page, testInfo, "admin-sanctions");

  await page.getByRole("row", { name: /lucia_g/ }).getByRole("button", { name: t("action.lift") }).click();
  expect((await readSent(page, "AdminRun")).at(-1)).toEqual(["/aunmute 3"]);
});

test("the audit, a page at a time", async ({ page }, testInfo) => {
  await page.goto("/admin/index.html#audit");
  await expect(page.locator(".admin-entry")).toHaveCount(25);
  await screenshot(page, testInfo, "admin-audit");

  await page.getByRole("button", { name: t("audit.older") }).click();
  await expect(page.locator(".admin-entry")).toHaveCount(40);
  await expect(page.getByRole("button", { name: t("audit.older") })).toHaveCount(0);
  const list = page.locator(".admin-audit > .admin-entries");
  const scrolled = await list.evaluate((element) => {
    element.scrollTop = element.scrollHeight;
    return element.scrollTop > 0;
  });
  expect(scrolled).toBe(true);
});

test("the roll log, a page at a time and by a character's name", async ({ page }, testInfo) => {
  await page.goto("/admin/index.html#dice");
  await expect(tab(page, "dice")).toHaveAttribute("aria-selected", "true");
  await expect(page.locator(".admin-roll")).toHaveCount(25);
  await expect(page.locator(".admin-roll").first()).toContainText("Marius Varro");
  await expectNoHorizontalOverflow(page, ".admin-dice");
  await screenshot(page, testInfo, "admin-dice");

  await page.getByRole("button", { name: t("dice.older") }).click();
  await expect(page.locator(".admin-roll")).toHaveCount(34);
  await expect(page.getByRole("button", { name: t("dice.older") })).toHaveCount(0);

  await page.getByLabel(t("dice.player")).fill("brannoc");
  await page.getByRole("button", { name: t("dice.search") }).click();
  await expect(page.locator(".admin-roll")).toHaveCount(8);
  await expect(page.locator(".admin-roll").first()).toContainText("Brannoc");
  expect(JSON.parse((await readSent(page, "AdminAsk")).at(-1)[1])).toEqual({ player: "brannoc", before: null });
  await screenshot(page, testInfo, "admin-dice-filtered");
});

test("the world: the sky of the area, the places and the traders", async ({ page }, testInfo) => {
  await page.goto("/admin/index.html#world");
  await expect(page.getByRole("heading", { name: t("world.places") })).toBeVisible();
  await expectNoHorizontalOverflow(page, ".admin__panel");
  await screenshot(page, testInfo, "admin-world");

  await page.getByLabel(t("world.places_filter")).fill("bru");
  await page.locator('[data-place="bruma-east-gate"]').click();
  expect((await readSent(page, "AdminRun")).at(-1)).toEqual(["/atp bruma-east-gate"]);
  await page.getByRole("button", { name: t("weather.thunderstorm") }).click();
  expect((await readSent(page, "AdminRun")).at(-1)).toEqual(["/aweather thunderstorm"]);

  const traders = page.locator(".admin-world__traders li");
  await expect(traders.nth(0).getByRole("button", { name: t("action.go") })).toBeVisible();
  await expect(traders.nth(1)).toContainText(t("world.elsewhere"));

  const spot = page.locator(".admin-world__spot");
  for (const [axis, value] of [["x", "100"], ["y", "-200"], ["z", "300"]]) await spot.getByLabel(axis, { exact: true }).fill(value);
  await spot.getByRole("button", { name: t("action.go") }).click();
  expect((await readSent(page, "AdminRun")).at(-1)).toEqual(["/atp 100 -200 300"]);
  await screenshot(page, testInfo, "admin-world-used");
});

test("the items: a kind, a few letters, how many and for whom, then Give", async ({ page }, testInfo) => {
  await page.goto("/admin/index.html#items");
  const panel = page.getByRole("tabpanel", { name: t("tab.items") });
  await expect(panel.getByText(t("items.start"))).toBeVisible();
  await expectNoHorizontalOverflow(page, ".admin__panel");
  await screenshot(page, testInfo, "admin-items-empty");

  await panel.getByRole("button", { name: t("items.type.WEAP") }).click();
  await expect(panel.locator(".admin-stock__found li")).toHaveCount(7);
  await panel.getByLabel(t("items.search")).fill("long");
  await expect(panel.locator(".admin-stock__found li")).toHaveCount(4);
  await panel.getByRole("group", { name: t("items.quick") }).getByRole("button", { name: "5", exact: true }).click();
  await panel.locator('[data-item="WeapSteelLongsword"]').click();
  expect((await readSent(page, "AdminRun")).at(-1)).toEqual(["/aitem give 1 WeapSteelLongsword 5"]);
  await expect(panel.locator(".admin-stock__recent")).toContainText("Steel Longsword");

  await panel.getByLabel(t("items.for")).selectOption("3");
  await panel.locator(".admin-stock__recent").getByRole("button", { name: t("action.give") }).click();
  expect((await readSent(page, "AdminRun")).at(-1)).toEqual(["/aitem give 3 WeapSteelLongsword 5"]);
  await expectNoHorizontalOverflow(page, ".admin__panel");
  await screenshot(page, testInfo, "admin-items");
});

test("a card's health and message ask first, a ban offers its common lengths", async ({ page }, testInfo) => {
  await page.goto("/admin/index.html");
  await page.locator('[data-player="3"]').click();
  const card = page.getByRole("region", { name: /\[3\] Lucia/ });
  await expect(card.getByRole("button", { name: t("action.revive") })).toHaveCount(0);

  await card.getByRole("button", { name: t("action.health") }).click();
  const dialog = page.getByRole("dialog");
  await expect(dialog.getByLabel("Health, from 1 to 90")).toBeFocused();
  await dialog.getByLabel("Health, from 1 to 90").fill("30");
  await page.keyboard.press("Enter");
  expect((await readSent(page, "AdminRun")).at(-1)).toEqual(["/ahp 3 30"]);

  await card.getByRole("button", { name: t("action.ban") }).click();
  await page.getByRole("dialog").getByRole("button", { name: t("dialog.preset.1440") }).click();
  await expect(page.getByRole("dialog").getByLabel(t("field.minutes"))).toHaveValue("1440");
  await screenshot(page, testInfo, "admin-ban-presets");
});

test("the spawn: none yet, set where the staff member stands, then gone to", async ({ page }, testInfo) => {
  await page.goto("/admin/index.html#nospawn");
  const spawn = page.locator(".admin-world__spawn");
  await expect(spawn).toContainText(t("world.no_spawn"));
  await expect(spawn.getByRole("button", { name: t("action.go") })).toHaveCount(0);
  await expectNoHorizontalOverflow(page, ".admin__panel");
  await screenshot(page, testInfo, "admin-spawn-none");

  await spawn.getByRole("button", { name: t("action.spawn_here") }).click();
  expect((await readSent(page, "AdminRun")).at(-1)).toEqual(["/aspawn here"]);
  await expect(spawn.locator(".admin-world__spawn-at")).toHaveText("At 26002 -90418 1198 cm");
  await screenshot(page, testInfo, "admin-spawn-set");

  await spawn.getByRole("button", { name: t("action.go") }).click();
  expect((await readSent(page, "AdminRun")).at(-1)).toEqual(["/aspawn go"]);
});

test("a player's lore card, read on request, a field cleared on the second click", async ({ page }, testInfo) => {
  await page.goto("/admin/index.html#card");
  const card = page.getByRole("region", { name: /\[4\] Brannoc/ });
  await card.getByRole("button", { name: t("card.lore_read") }).click();
  const lore = card.getByRole("region", { name: t("card.lore") });
  await expect(lore.locator(".lore-view__field")).toHaveCount(9);
  await lore.locator('[data-field="notes"]').scrollIntoViewIfNeeded();
  await expectNoHorizontalOverflow(page, ".admin-card");
  await screenshot(page, testInfo, "admin-lore");

  await lore.locator('[data-clear="notes"]').click();
  await expect(lore.locator('[data-clear="notes"]')).toHaveText(t("card.lore_clear_again"));
  expect(await readSent(page, "AdminRun")).toEqual([]);
  await lore.locator('[data-clear="notes"]').click();
  expect(await readSent(page, "AdminRun")).toEqual([["/alore 4 clear notes"]]);
  await expect(lore.locator(".lore-view__field")).toHaveCount(8);
  await expect(lore.locator('[data-field="notes"]')).toHaveCount(0);
});

test("the Sanctuary: on or off, the aggressors with a streak to end, the incidents by pages and names", async ({ page }, testInfo) => {
  await page.goto("/admin/index.html#sanctuary");
  await expect(tab(page, "sanctuary")).toHaveAttribute("aria-selected", "true");
  await expect(page.locator(".admin-sanctuary__running")).toHaveText(t("sanctuary.on"));
  await expect(page.locator(".admin-sanctuary__aggressor")).toHaveCount(2);
  await expect(page.locator(".admin-incident")).toHaveCount(25);
  await expectNoHorizontalOverflow(page, ".admin-sanctuary");
  await screenshot(page, testInfo, "admin-sanctuary");

  await page.locator('.admin-sanctuary__aggressor[data-player="12"]').getByRole("button", { name: t("action.sanctuaryClear") }).click();
  expect((await readSent(page, "AdminRun")).at(-1)).toEqual(["/asanctuary 12 clear"]);
  await expect(page.locator(".admin-sanctuary__aggressor")).toHaveCount(1);

  await page.getByRole("button", { name: t("sanctuary.older") }).click();
  await expect(page.locator(".admin-incident")).toHaveCount(31);
  await expect(page.getByRole("button", { name: t("sanctuary.older") })).toHaveCount(0);

  await page.getByLabel(t("sanctuary.name")).fill("hrodulf");
  await page.getByRole("button", { name: t("sanctuary.search") }).click();
  expect(JSON.parse((await readSent(page, "AdminAsk")).at(-1)[1])).toEqual({ name: "hrodulf", before: null });
  await expect(page.locator(".admin-incident").first()).toContainText("Hrodulf");

  await page.getByRole("button", { name: t("sanctuary.turn_off") }).click();
  await expect(page.locator(".admin-sanctuary__running")).toHaveText(t("sanctuary.off"));
  await expect(page.locator(".admin-sanctuary")).toContainText(t("sanctuary.no_aggressors"));
  await screenshot(page, testInfo, "admin-sanctuary-off");
});
