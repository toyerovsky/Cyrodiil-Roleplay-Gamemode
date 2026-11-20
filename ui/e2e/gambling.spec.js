import { test, expect, readSent, readTexts, screenshot, expectNoHorizontalOverflow, pressInGame } from "./fixtures.js";

const t = readTexts("ui.gambling");

test("choose Poker first, invite, raise, pick dice and read the result", async ({ page }, testInfo) => {
  await page.goto("/gambling/index.html");
  await expect(page.getByRole("button", { name: t("Poker"), exact: true })).toHaveAttribute("aria-pressed", "true");
  await expectNoHorizontalOverflow(page, ".gambling__panel");
  await page.getByLabel(t("stake")).fill("40");
  await screenshot(page, testInfo, "gambling-picker");
  await page.getByRole("button", { name: t("invite"), exact: true }).click();
  await expect(page.getByLabel(t("raise_amount"))).toBeVisible();
  expect(await readSent(page, "GamblingInvite")).toEqual([[3, 40, "Poker"]]);
  await screenshot(page, testInfo, "gambling-raise");
  await page.getByLabel(t("raise_amount")).fill("10");
  await page.getByRole("button", { name: t("raise"), exact: true }).click();
  await expect(page.getByRole("button", { name: t("confirm"), exact: true })).toBeEnabled();
  await page.getByRole("button", { name: "Die 1: 4", exact: true }).click();
  await pressInGame(page, 53);
  await expect(page.getByRole("button", { name: "Die 5: 6", exact: true })).toHaveAttribute("aria-pressed", "true");
  await expectNoHorizontalOverflow(page, ".gambling__hands");
  await screenshot(page, testInfo, "gambling-reroll");
  await pressInGame(page, 32);
  await expect(page.getByText("You won the match. The pot is on its way.", { exact: true })).toBeVisible();
  expect((await readSent(page, "GamblingAct")).at(-1).slice(2)).toEqual(["Pick", 17]);
  await screenshot(page, testInfo, "gambling-result");
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
  await expect(page.getByRole("button", { name: "Call 10 gold", exact: true })).toBeVisible();
  await screenshot(page, testInfo, "gambling-call");
  await page.keyboard.press("Escape");
  await expect(page.locator(".gambling")).toHaveCount(0);
  expect(await readSent(page, "GamblingAct")).toEqual([]);
  expect(await readSent(page, "GamblingClose")).toEqual([[]]);
  await page.goto("/gambling/index.html?fold#call");
  await page.getByRole("button", { name: t("fold"), exact: true }).click();
  await expect(page.getByText("You lost the match.", { exact: true })).toBeVisible();
});

test("Favor shows two dice per player and no raise controls", async ({ page }, testInfo) => {
  await page.goto("/gambling/index.html#favor");
  await expect(page.locator(".gambling__die")).toHaveCount(4);
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
