import { test, expect, readTexts, screenshot, readSent, pauseClock, expectNoHorizontalOverflow, pressInGame } from "./fixtures.js";

const t = readTexts("ui.fisher");
const KEY_Q = 81;
const KEY_E = 69;

async function openFisher(page, scenario = "") {
  await page.goto(`/fisher/index.html${scenario ? `#${scenario}` : ""}`);
  await expect(page.getByRole("region", { name: t("title") })).toBeVisible();
}

// The picked row's details and its buttons fit their pane, with nothing to scroll to.
async function expectDetailWhole(page) {
  const hidden = await page.locator(".fisher__detail").evaluate((detail) => detail.scrollHeight - detail.clientHeight);
  expect(hidden, "the detail pane should show all it holds").toBeLessThanOrEqual(1);
}

const tab = (page, key) => page.getByRole("tab", { name: t(`tab.${key}`) });
const rows = (page, key) => page.getByRole("listbox", { name: t(`list.${key}`) });
const species = (page, name) => page.getByRole("option", { name });
const landed = (page) => page.locator("[data-records] tbody tr");
const cells = (row) => row.locator("td").allInnerTexts();

test("the catch is sold fish by fish and then whole, the purse following", async ({ page }, testInfo) => {
  await openFisher(page);
  await expect(rows(page, "catch")).toBeFocused();
  await expect(page.getByRole("option", { name: /Silverside Perch/ })).toHaveAttribute("aria-selected", "true");
  await expect(page.locator("[data-purse]")).toHaveText("120 gold");
  await expectNoHorizontalOverflow(page, ".fisher__panel");
  await expectDetailWhole(page);
  await screenshot(page, testInfo, "fisher-catch");

  await page.keyboard.press("Enter");
  await expect(page.locator(".fisher__status")).toHaveText("Sold Silverside Perch × 1 for 2 gold.");
  await expect(page.locator("[data-purse]")).toHaveText("122 gold");
  await expect(page.getByRole("option", { name: /Silverside Perch/ })).toContainText("×4");

  await page.getByRole("option", { name: /Rumare Carp/ }).click();
  await page.getByRole("button", { name: "Sell all 2 for 8 gold" }).click();
  await expect(page.locator("[data-purse]")).toHaveText("130 gold");
  await expect(page.getByRole("option", { name: /Rumare Carp/ })).toHaveCount(0);
  await expect(page.getByRole("option", { name: /Niben Eel/ })).toHaveAttribute("aria-selected", "true");
  await screenshot(page, testInfo, "fisher-sold");

  await page.getByRole("button", { name: "Sell the whole catch for 34 gold" }).click();
  await expect(page.locator("[data-purse]")).toHaveText("164 gold");
  await expect(page.locator("[data-empty]")).toContainText(t("empty.catch"));
  await expect(tab(page, "catch")).toBeFocused();
  await screenshot(page, testInfo, "fisher-catch-empty");
  expect(await readSent(page, "FisherSell")).toEqual([["fishing.perch", 1], ["fishing.carp", 2]]);
  expect(await readSent(page, "FisherSellAll")).toEqual([[]]);
});

test("bait is bought from the keyboard: E to the shop, the arrows, a count and Enter", async ({ page }, testInfo) => {
  await openFisher(page);
  await pressInGame(page, KEY_E);
  await expect(tab(page, "shop")).toHaveAttribute("aria-selected", "true");
  await expect(rows(page, "shop")).toBeFocused();
  await expect(page.locator("[data-rod-facts]")).toHaveText("casts 7 m · line strength 100%");
  await expectDetailWhole(page);
  await screenshot(page, testInfo, "fisher-shop-rod");

  await page.keyboard.press("End");
  await page.keyboard.press("ArrowUp");
  await page.keyboard.press("ArrowUp");
  await expect(page.getByRole("option", { name: /Fishing Worms/ })).toHaveAttribute("aria-selected", "true");
  await expect(page.locator("[data-carried]")).toHaveText("You carry 12");
  for (let i = 0; i < 9; i++) await page.keyboard.press("+");
  await expect(page.locator(".fisher__amount")).toHaveText("10");
  await expect(page.locator(".fisher__total")).toHaveText("Total 10 gold");
  await expectDetailWhole(page);
  await screenshot(page, testInfo, "fisher-shop-worms");
  await page.keyboard.press("Enter");
  await expect(page.locator(".fisher__status")).toHaveText("Bought Fishing Worms × 10 for 10 gold.");
  await expect(page.locator("[data-purse]")).toHaveText("110 gold");
  await expect(page.locator("[data-carried]")).toHaveText("You carry 22");
  expect(await readSent(page, "FisherBuy")).toEqual([["fishing.worms", 10]]);

  await pressInGame(page, KEY_Q);
  await pressInGame(page, KEY_Q);
  await expect(tab(page, "records")).toHaveAttribute("aria-selected", "true");
  await pressInGame(page, KEY_Q);
  await expect(tab(page, "repair")).toHaveAttribute("aria-selected", "true");
});

test("a worn rod is mended, and a dear one cannot be", async ({ page }, testInfo) => {
  await openFisher(page);
  await tab(page, "repair").click();
  await expect(page.locator(".fisher__detail")).toContainText("Mended as good as new for 4 gold.");
  await expectDetailWhole(page);
  await screenshot(page, testInfo, "fisher-repair");

  await page.getByRole("button", { name: t("repair"), exact: true }).click();
  await expect(page.locator("[data-purse]")).toHaveText("116 gold");
  await expect(page.getByRole("option", { name: /Fishing Rod \(Worn\)/ })).toHaveCount(0);
  await page.getByRole("option", { name: /Master Fishing Rod/ }).click();
  await expect(page.locator('[data-action="repair"]')).toBeDisabled();
  await expect(page.locator('[data-action="repair"]')).toHaveText(t("too_poor"));
  expect(await readSent(page, "FisherRepair")).toEqual([["fishing.rod.worn"]]);
});

test("with a thin purse the dear wares and repairs are marked and cannot be bought", async ({ page }, testInfo) => {
  await openFisher(page, "poor");
  await tab(page, "shop").click();
  await expect(page.locator(".fisher__row--dear")).toHaveCount(4);
  await expect(page.locator('[data-action="buy"]')).toBeDisabled();
  await screenshot(page, testInfo, "fisher-poor-shop");

  await tab(page, "repair").click();
  await expect(page.locator(".fisher__row--dear")).toHaveCount(3);
  await screenshot(page, testInfo, "fisher-poor-repair");
});

test("every tab says so when it has nothing, and the tabs keep the keys", async ({ page }, testInfo) => {
  await openFisher(page, "empty");
  await expect(tab(page, "catch")).toBeFocused();
  await expect(page.locator(".fisher__status")).toHaveText(t("hint.empty"));
  for (const key of ["catch", "shop", "repair"]) {
    await tab(page, key).click();
    await expect(page.locator("[data-empty]")).toContainText(t(`empty.${key}`));
    await expectNoHorizontalOverflow(page, ".fisher__panel");
    await screenshot(page, testInfo, `fisher-empty-${key}`);
  }

  await page.keyboard.press("ArrowLeft");
  await expect(tab(page, "shop")).toBeFocused();
});

test("a long catch scrolls in its list, the picked fish kept in sight", async ({ page }, testInfo) => {
  await openFisher(page, "many");
  await expect(page.getByRole("option")).toHaveCount(26);
  await expect(page.locator('[data-action="sell-all-of"]')).toHaveText("Sell 20 for 40 gold");
  await page.keyboard.press("End");
  await expect(page.getByRole("option", { name: /Trophy Emperor's Golden Carp/ })).toHaveAttribute("aria-selected", "true");
  await expect.poll(() => rows(page, "catch").evaluate((list) => list.scrollTop)).toBeGreaterThan(0);
  await expect(page.getByRole("option", { name: /Trophy Emperor's Golden Carp/ })).toBeInViewport();
  await expectDetailWhole(page);
  await expectNoHorizontalOverflow(page, ".fisher__rows");
  await screenshot(page, testInfo, "fisher-many");
});

test("the records are read from the keyboard: Q to the board, the arrows through the species", async ({ page }, testInfo) => {
  await page.clock.install();
  await page.goto("/fisher/index.html");
  await page.clock.runFor(100);
  await expect(rows(page, "catch")).toBeFocused();
  await pauseClock(page);

  await pressInGame(page, KEY_Q);
  await expect(tab(page, "records")).toHaveAttribute("aria-selected", "true");
  await expect(page.locator("[data-empty]")).toHaveText(t("empty.records"));
  await expect(tab(page, "records")).toBeFocused();
  await screenshot(page, testInfo, "fisher-records-reading");

  await page.clock.runFor(100);
  await expect(rows(page, "records")).toBeFocused();
  await expect(page.getByRole("option")).toHaveCount(13);
  await expect(species(page, /Silverside Perch/)).toHaveAttribute("aria-selected", "true");
  await expect(species(page, /Silverside Perch/)).toContainText("35 cm");
  await expect(landed(page)).toHaveCount(5);
  expect(await cells(landed(page).nth(0))).toEqual(["1.", "Jauffre Valtieri", "35 cm", "549 g", "Nov 18, 2026"]);
  expect(await cells(landed(page).nth(1))).toEqual(["2.", `Marius Caro ${t("records.you")}`, "34 cm", "503 g", "Nov 25, 2026"]);
  expect((await cells(landed(page).nth(2)))[0]).toBe("3.");
  expect((await cells(landed(page).nth(3)))[0]).toBe("3.");
  expect((await cells(landed(page).nth(4))).slice(0, 2)).toEqual(["5.", "Stranger"]);
  await expect(page.locator("[data-records] tbody tr[data-mine]")).toHaveCount(1);
  await expect(page.locator("[data-own]")).toHaveCount(0);
  await expect(page.locator(".fisher__status")).toHaveText(t("hint.records"));
  await expectNoHorizontalOverflow(page, ".fisher__panel");
  await expectNoHorizontalOverflow(page, ".fisher__detail");
  await expectDetailWhole(page);
  await screenshot(page, testInfo, "fisher-records");

  await page.keyboard.press("ArrowDown");
  await expect(species(page, /River Betty/)).toHaveAttribute("aria-selected", "true");
  await expect(page.locator("[data-no-records]")).toHaveText(t("records.none"));
  await expect(page.locator("[data-records]")).toHaveCount(0);
  await screenshot(page, testInfo, "fisher-records-none");

  for (let i = 0; i < 4; i++) await page.keyboard.press("ArrowDown");
  await expect(species(page, /^Pike/)).toHaveAttribute("aria-selected", "true");
  await expect(landed(page)).toHaveCount(5);
  await expect(page.locator("[data-records] tbody tr[data-mine]")).toHaveCount(0);
  expect(await cells(landed(page).nth(0))).toEqual(["1.", "Bosmer", "108 cm", "10.5 kg", "Nov 15, 2026"]);
  expect(await cells(page.locator("[data-own]"))).toEqual(["7.", t("records.own"), "74 cm", "3.4 kg", "Nov 24, 2026"]);
  await expectDetailWhole(page);
  await screenshot(page, testInfo, "fisher-records-own");

  await page.keyboard.press("Enter");
  await page.keyboard.press("End");
  await expect(species(page, /Emperor's Golden Carp/)).toHaveAttribute("aria-selected", "true");
  await expect(species(page, /Emperor's Golden Carp/)).toBeInViewport();
  await expect(page.locator("[data-no-records]")).toBeVisible();

  await pressInGame(page, KEY_E);
  await expect(tab(page, "catch")).toHaveAttribute("aria-selected", "true");
  await pressInGame(page, KEY_Q);
  await expect(rows(page, "records")).toBeFocused();
  expect(await readSent(page, "FisherAskRecords")).toEqual([[], []]);
  expect(await readSent(page, "FisherSell")).toEqual([]);
  expect(await readSent(page, "FisherRepair")).toEqual([]);
});

test("the records open with the mouse too, and read in Polish", async ({ page }, testInfo) => {
  await openFisher(page);
  await tab(page, "records").click();
  await species(page, /Colovian Trout/).click();
  await expect(landed(page)).toHaveCount(5);
  expect((await cells(page.locator("[data-own]"))).slice(0, 3)).toEqual(["12.", t("records.own"), "38 cm"]);

  await page.evaluate(() => window.__readym.emit("Locale", "pl"));
  const pl = readTexts("ui.fisher", "pl");
  await expect(page.getByRole("tab", { name: pl("tab.records") })).toHaveAttribute("aria-selected", "true");
  await expect(page.locator("[data-records] th")).toHaveText(["rank", "name", "length", "weight", "date"].map((column) => pl(`records.column.${column}`)));
  expect(await cells(landed(page).nth(0))).toEqual(["1.", "Dar-Ma", "54 cm", "1,9 kg", "24 lis 2026"]);
  expect((await cells(page.locator("[data-own]")))[1]).toBe(pl("records.own"));
  await expectNoHorizontalOverflow(page, ".fisher__panel");
  await expectNoHorizontalOverflow(page, ".fisher__detail");
  await expectDetailWhole(page);
  await screenshot(page, testInfo, "fisher-records-polish");
});

test("on a crowded board a long name wraps in its row and the species scroll in their list", async ({ page }, testInfo) => {
  await openFisher(page, "records");
  await tab(page, "records").click();
  await expect(landed(page)).toHaveCount(5);
  await expect(landed(page).nth(1)).toContainText("Maximilianus-Aurelianus-Septimius-Corvus Vandersteen-Montclair-Albrechtsberger");
  await expect(page.locator("[data-records] tbody tr[data-mine]")).toContainText("Marius Caro");
  await expectNoHorizontalOverflow(page, ".fisher__panel");
  await expectNoHorizontalOverflow(page, ".fisher__detail");
  await expectNoHorizontalOverflow(page, ".fisher__rows");
  await screenshot(page, testInfo, "fisher-records-crowded");

  await rows(page, "records").focus();
  await page.keyboard.press("ArrowDown");
  await expect(species(page, /River Betty/)).toHaveAttribute("aria-selected", "true");
  expect((await cells(page.locator("[data-own]"))).slice(0, 2)).toEqual(["1,205.", t("records.own")]);
  await expectNoHorizontalOverflow(page, ".fisher__detail");
  await expectDetailWhole(page);
  await screenshot(page, testInfo, "fisher-records-crowded-own");

  await page.keyboard.press("End");
  await expect(species(page, /Emperor's Golden Carp/)).toHaveAttribute("aria-selected", "true");
  await expect.poll(() => rows(page, "records").evaluate((list) => list.scrollTop)).toBeGreaterThan(0);
  await expect(species(page, /Emperor's Golden Carp/)).toBeInViewport();
  await screenshot(page, testInfo, "fisher-records-crowded-end");
});

test("a server where nobody has fished yet says so for every species", async ({ page }, testInfo) => {
  await openFisher(page, "norecords");
  await tab(page, "records").click();
  await expect(page.getByRole("option")).toHaveCount(13);
  await expect(page.locator(".fisher__row--none")).toHaveCount(13);
  await expect(page.locator("[data-no-records]")).toHaveText(t("records.none"));
  await expectDetailWhole(page);
  await screenshot(page, testInfo, "fisher-records-nobody");
});

test("a board the fisher cannot read says so, and the window goes on", async ({ page }, testInfo) => {
  await openFisher(page, "unread");
  await pressInGame(page, KEY_Q);
  await expect(page.locator("[data-empty]")).toHaveText(t("records.unread"));
  await expect(page.locator(".fisher__status--refused")).toContainText("cannot find the book of records");
  await expect(tab(page, "records")).toBeFocused();
  await screenshot(page, testInfo, "fisher-records-unread");

  await pressInGame(page, KEY_E);
  await expect(tab(page, "catch")).toBeFocused();
  await page.getByRole("button", { name: t("sell_one") }).click();
  await expect(page.locator(".fisher__status")).toHaveText("Sold Silverside Perch × 1 for 2 gold.");
});

test("the focus shows on every control Tab reaches", async ({ page }, testInfo) => {
  await openFisher(page);
  await page.keyboard.press("Tab");
  await expect(page.locator('[data-action="sell-everything"]')).toBeFocused();
  await page.keyboard.press("Tab");
  await expect(page.locator('[data-action="sell-one"]')).toBeFocused();
  const glow = await page.locator('[data-action="sell-one"]').evaluate((button) => getComputedStyle(button).filter);
  expect(glow).toContain("drop-shadow");
  await screenshot(page, testInfo, "fisher-focus");
});

test("Escape and Leave close the fisher's window", async ({ page }) => {
  await openFisher(page);
  await page.keyboard.press("Escape");
  await expect(page.locator(".fisher")).toHaveCount(0);
  expect(await readSent(page, "FisherClose")).toEqual([[]]);

  await page.goto("/fisher/index.html?shot=leave");
  await page.getByRole("button", { name: t("close") }).click();
  await expect(page.locator(".fisher")).toHaveCount(0);
});
