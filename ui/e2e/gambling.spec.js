import { test, expect, readSent, readTexts, screenshot, expectNoHorizontalOverflow, pressInGame, pauseClock } from "./fixtures.js";

const t = readTexts("ui.gambling");
// Two boards bring three.js, their models and two WebGL contexts drawn in software here: in a busy preview server the
// table takes longer than the usual few seconds to show, as the dice page does (dice.spec.js).
const PAGE_MS = 20_000;
// A player clicks a button at least a frame after it appears. On a paused clock the answer and the click would share a
// millisecond, and Vue ignores an event as old as its listener. A board whose dice finished loading after the clock
// stopped also draws them only in the next frame, so a screenshot on a paused clock runs one frame first.
const FRAME_MS = 16;

// Every frame of a fake clock's run is drawn in software, two boards at a time: a whole match plays six throws.
test.describe.configure({ timeout: 90_000 });

test("shared boards play initial throws, live picks, a partial reroll and the complete match", async ({ page }, testInfo) => {
  test.setTimeout(180_000);
  await page.clock.install();
  await page.goto("/gambling/index.html");
  await expect(page.getByRole("button", { name: t("Poker"), exact: true })).toHaveAttribute("aria-pressed", "true");
  await pauseClock(page);
  await expectNoHorizontalOverflow(page, ".gambling__panel");
  await page.getByLabel(t("stake")).fill("40");
  await screenshot(page, testInfo, "gambling-picker");
  await page.getByRole("button", { name: t("invite"), exact: true }).click();
  await page.clock.runFor(650);
  await expect(page.locator('.gambling__board[data-seat="0"]')).toHaveAttribute("data-phase", "flight", { timeout: PAGE_MS });
  await expect(page.getByRole("status")).toContainText("Now rolling: Marcus Varro");
  await page.clock.runFor(3000);
  await expect(page.getByRole("status")).toContainText("Now rolling: A wandering Breton from the mountains above Chorrol");
  await page.clock.runFor(3016);
  await expect(page.getByLabel(t("raise_amount"))).toBeVisible();
  await expect(page.locator('.gambling__board[data-phase="rest"]')).toHaveCount(2);
  for (const canvas of await page.locator(".gambling__board canvas").all()) await expect(canvas).toBeInViewport({ ratio: 1 });
  await expect(page.getByRole("button", { name: t("hide"), exact: true })).toBeInViewport();
  expect(await readSent(page, "GamblingInvite")).toEqual([[3, 40, "Poker"]]);
  await expect(page.locator(".gambling__hand").first()).toHaveAttribute("data-seat", "0");
  await expectNoHorizontalOverflow(page, ".gambling__hands");
  await page.clock.runFor(FRAME_MS);
  await screenshot(page, testInfo, "gambling-both-first-throws");
  await page.getByLabel(t("raise_amount")).fill("10");
  await page.getByRole("button", { name: t("raise"), exact: true }).click();
  await page.clock.runFor(750);
  await expect(page.getByRole("button", { name: t("confirm"), exact: true })).toBeEnabled();
  await page.clock.runFor(1200);
  await expect(page.locator('.gambling__board[data-seat="1"]')).toHaveAttribute("data-picked", "8");
  await page.clock.runFor(FRAME_MS);
  await screenshot(page, testInfo, "gambling-opponent-live-pick");
  await page.getByRole("button", { name: "Die 1: 4", exact: true }).click();
  await pressInGame(page, 53);
  await expect(page.getByRole("button", { name: "Die 5: 6", exact: true }).first()).toHaveAttribute("aria-pressed", "true");
  await pressInGame(page, 32);
  expect((await readSent(page, "GamblingAct")).at(-1).slice(2)).toEqual(["Pick", 17]);
  await page.clock.runFor(1450);
  await expect(page.locator('.gambling__board[data-seat="0"]')).toHaveAttribute("data-phase", "flight");
  await page.clock.runFor(1000);
  await screenshot(page, testInfo, "gambling-partial-reroll-flight");
  await page.clock.runFor(5016);
  await expect(page.getByRole("status")).toContainText("Marcus Varro wins the round");
  await page.clock.runFor(FRAME_MS);
  await screenshot(page, testInfo, "gambling-round-result");
  await page.clock.runFor(10016);
  await page.getByRole("button", { name: t("pass"), exact: true }).click();
  await page.clock.runFor(50 + FRAME_MS);
  await page.getByRole("button", { name: "Call 10 gold", exact: true }).click();
  await page.clock.runFor(50 + FRAME_MS);
  await expect(page.getByRole("button", { name: t("confirm"), exact: true })).toBeEnabled();
  await pressInGame(page, 51);
  await expect(page.getByRole("button", { name: "Die 3: 1", exact: true })).toHaveAttribute("aria-pressed", "true");
  await pressInGame(page, 13);
  expect((await readSent(page, "GamblingAct")).at(-1).slice(2)).toEqual(["Pick", 4]);
  await page.clock.runFor(7416);
  await expect(page.getByRole("status")).toContainText("You won the match. The pot is on its way.");
  await expect(page.locator('.gambling__board[data-phase="rest"]')).toHaveCount(2);
  await page.clock.runFor(FRAME_MS);
  await screenshot(page, testInfo, "gambling-match-result");
});

test("Esc hides a live throw and the preview G reopens its final poses without replay", async ({ page }, testInfo) => {
  test.setTimeout(90_000);
  await page.clock.install();
  await page.goto("/gambling/index.html");
  await expect(page.getByRole("button", { name: t("invite"), exact: true })).toBeVisible();
  await pauseClock(page);
  await page.getByRole("button", { name: t("invite"), exact: true }).click();
  await page.clock.runFor(650);
  await expect(page.locator('.gambling__board[data-seat="0"]')).toHaveAttribute("data-phase", "flight", { timeout: PAGE_MS });
  await page.keyboard.press("Escape");
  await page.clock.runFor(50);
  await expect(page.locator(".gambling")).toHaveCount(0);
  await page.clock.runFor(1000);
  await page.keyboard.press("g");
  await expect(page.locator('.gambling__board[data-seat="0"]')).toHaveAttribute("data-phase", "rest");
  await page.clock.runFor(16);
  await screenshot(page, testInfo, "gambling-reopened-at-rest");
  await page.clock.runFor(2000);
  await expect(page.locator('.gambling__board[data-seat="1"]')).toHaveAttribute("data-phase", "flight");
});

test("seat one sees their own negative board above seat zero and a relayed pick", async ({ page }, testInfo) => {
  await page.goto("/gambling/index.html#seat1");
  await expect(page.locator('.gambling__board[data-phase="rest"]')).toHaveCount(2, { timeout: PAGE_MS });
  const hands = page.locator(".gambling__hand");
  await expect(hands.first()).toHaveAttribute("data-seat", "1");
  await expect(hands.first().locator(".gambling__board")).toHaveAttribute("data-board", "board-negative");
  await expect(hands.last().locator(".gambling__board")).toHaveAttribute("data-board", "board");
  await expect(hands.last().locator(".gambling__board")).toHaveAttribute("data-picked", "8");
  await expectNoHorizontalOverflow(page, ".gambling__panel");
  await screenshot(page, testInfo, "gambling-seat-one-layout");
});

test("read all rules and scroll inside the window", async ({ page }, testInfo) => {
  await page.goto("/gambling/index.html#rules");
  await expect(page.locator(".gambling__ranks li")).toHaveCount(9);
  await expectNoHorizontalOverflow(page, ".gambling__content");
  await screenshot(page, testInfo, "gambling-rules-top");
  await page.locator(".gambling__content").evaluate((element) => element.scrollTo(0, element.scrollHeight));
  await expect(page.locator(".gambling__rules p").last()).toBeInViewport();
  await expect(page.getByRole("button", { name: t("hide"), exact: true })).toBeInViewport();
  await screenshot(page, testInfo, "gambling-rules-bottom");
});

test("call, fold and hide are separate decisions", async ({ page }, testInfo) => {
  await page.goto("/gambling/index.html#call");
  await expect(page.getByRole("button", { name: "Call 10 gold", exact: true })).toBeVisible({ timeout: PAGE_MS });
  await expect(page.locator('.gambling__board[data-phase="rest"]')).toHaveCount(2, { timeout: PAGE_MS });
  await screenshot(page, testInfo, "gambling-call");
  await page.keyboard.press("Escape");
  await expect(page.locator(".gambling")).toHaveCount(0);
  expect(await readSent(page, "GamblingAct")).toEqual([]);
  expect(await readSent(page, "GamblingClose")).toEqual([[]]);
  await page.goto("/gambling/index.html?fold#call");
  await page.getByRole("button", { name: t("fold"), exact: true }).click({ timeout: PAGE_MS });
  await expect(page.getByRole("status").filter({ hasText: "You lost the match." })).toBeVisible({ timeout: PAGE_MS });
  expect((await readSent(page, "GamblingAct")).map((args) => args[2])).toEqual(["Fold"]);
});

test("Favor shows two dice per player and no raise controls", async ({ page }, testInfo) => {
  await page.goto("/gambling/index.html#favor");
  await expect(page.locator(".gambling__die")).toHaveCount(4, { timeout: PAGE_MS });
  await expect(page.locator('.gambling__board[data-phase="rest"]')).toHaveCount(2, { timeout: PAGE_MS });
  await expect(page.getByLabel(t("raise_amount"))).toHaveCount(0);
  await expectNoHorizontalOverflow(page, ".gambling__panel");
  await screenshot(page, testInfo, "gambling-favor");
});

test("the invitation card states the game and stake and answers Y", async ({ page }, testInfo) => {
  await page.goto("/asks/index.html#gambling");
  await expect(page.getByText("Dice Poker, stake: 40 gold.", { exact: true })).toBeVisible();
  await screenshot(page, testInfo, "gambling-invitation");
  await page.keyboard.press("y");
  await expect(page.locator(".ask")).toHaveCount(0);
});

test("F10 Dice history holds the last ten games", async ({ page }, testInfo) => {
  await page.goto("/menu/index.html#dice");
  await expect(page.locator(".gambling-history li")).toHaveCount(10);
  await page.locator(".menu__content").evaluate((element) => element.scrollTo(0, element.scrollHeight));
  await expect(page.locator(".gambling-history li").last()).toBeInViewport();
  await expectNoHorizontalOverflow(page, ".menu__content");
  await screenshot(page, testInfo, "gambling-menu-history");
});

test("staff filter fifty games, cancel one and page older games", async ({ page }, testInfo) => {
  await page.goto("/admin/index.html#gambling");
  await expect(page.locator(".gambling-history li")).toHaveCount(50);
  await expectNoHorizontalOverflow(page, ".admin__content");
  await page.getByRole("button", { name: "Cancel game #100", exact: true }).click();
  expect((await readSent(page, "AdminRun")).at(-1)).toEqual(["/agamble cancel 100"]);
  await expect(page.getByRole("button", { name: "Cancel game #100", exact: true })).toHaveCount(0);
  await screenshot(page, testInfo, "gambling-admin-cancelled");
  await page.getByLabel(t("filter")).fill("Lucia");
  await page.getByRole("button", { name: t("search"), exact: true }).click();
  await expect(page.locator(".gambling-history li")).toHaveCount(50);
  await page.getByRole("button", { name: t("older"), exact: true }).click();
  await expect(page.locator(".gambling-history li")).toHaveCount(5);
  await screenshot(page, testInfo, "gambling-admin-older");
});
