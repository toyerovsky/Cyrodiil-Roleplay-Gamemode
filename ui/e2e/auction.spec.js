import { test, expect, readTexts, screenshot, readSent, pauseClock, expectNoHorizontalOverflow } from "./fixtures.js";

const t = readTexts("ui.auction");

const openTab = (page, id) => page.getByRole("tab", { name: new RegExp(`^${t(`tab.${id}`)}`) }).click();
const lot = (page, name) => page.locator(".auction-browse__lots").getByRole("button", { name: new RegExp(name) });

test("a lot is found by name and kind, bid on, and another bought now", async ({ page }, testInfo) => {
  await page.goto("/auction/index.html");
  const window = page.getByRole("region", { name: t("title") });
  const status = page.getByRole("status");
  await expect(window).toBeVisible();
  await expect(page.getByLabel(t("search"))).toBeFocused();
  await expect(page.locator("[data-purse]")).toHaveText("640 gold");
  await expect(page.locator(".auction-browse__lots li")).toHaveCount(9);
  await expectNoHorizontalOverflow(page, ".auction__panel");
  await screenshot(page, testInfo, "auction-browse");

  await page.getByRole("group", { name: t("categories") }).getByRole("button", { name: t("category.Ingredients") }).click();
  await expect(page.locator(".auction-browse__lots li")).toHaveCount(1);
  await lot(page, "Nirnroot").click();
  const bid = page.getByLabel(t("your_bid"));
  await expect(bid).toHaveValue("73");
  await bid.fill("80");
  await page.getByRole("button", { name: t("bid"), exact: true }).click();
  await expect(status).toHaveText("You bid 80 gold on Nirnroot (3). The house keeps the gold until someone outbids you or you win.");
  await expect(page.locator("[data-purse]")).toHaveText("560 gold");
  await expect(lot(page, "Nirnroot")).toContainText(t("leading"));
  await screenshot(page, testInfo, "auction-bid");

  await page.getByRole("group", { name: t("categories") }).getByRole("button", { name: t("category.All") }).click();
  await page.getByLabel(t("search")).fill("dagger");
  await page.getByRole("button", { name: t("search_go") }).click();
  await expect(page.locator(".auction-browse__lots li")).toHaveCount(1);
  await lot(page, "Iron Dagger").click();
  await page.getByRole("button", { name: "Buy now for 60 gold" }).click();
  await expect(status).toHaveText("You bought Iron Dagger for 60 gold. Collect it at any auctioneer.");
  await expect(page.locator(".auction-browse__lots li")).toHaveCount(0);
  await expect(page.getByText(t("no_match"))).toBeVisible();
  await expect(page.getByRole("tab", { name: "Collect (4)" })).toBeVisible();

  expect(await readSent(page, "AuctionSearch")).toEqual([["", "Ingredients", 0], ["", "All", 0], ["dagger", "All", 0]]);
  expect(await readSent(page, "AuctionBid")).toEqual([[7, 80]]);
  expect(await readSent(page, "AuctionBuy")).toEqual([[1, 60]]);
});

test("a bid and a lot the house would refuse are marked at their fields and not sent", async ({ page }, testInfo) => {
  await page.goto("/auction/index.html");
  await lot(page, "Elven Longsword").click();
  await page.getByLabel(t("your_bid")).fill("530");
  await page.getByRole("button", { name: t("bid"), exact: true }).click();
  await expect(page.getByRole("alert")).toHaveText("Bid a whole sum from 546 to 100,000 gold.");
  await expect(page.getByLabel(t("your_bid"))).toHaveAttribute("aria-invalid", "true");
  await expect(page.getByLabel(t("your_bid"))).toBeFocused();
  await screenshot(page, testInfo, "auction-bid-refused");

  await openTab(page, "sell");
  await page.getByRole("button", { name: /Iron Arrow/ }).click();
  await page.getByLabel(t("start")).fill("90");
  await page.getByLabel(t("buyout")).fill("80");
  await page.getByRole("button", { name: t("post"), exact: true }).click();
  await expect(page.getByRole("alert")).toHaveText(t("buyout_low"));
  await expect(page.getByLabel(t("buyout"))).toHaveAttribute("aria-invalid", "true");
  await screenshot(page, testInfo, "auction-sell-refused");
  expect(await readSent(page, "AuctionBid")).toEqual([]);
  expect(await readSent(page, "AuctionPost")).toEqual([]);
});

test("an item is put up with its fee shown, and taken back from My lots", async ({ page }, testInfo) => {
  await page.goto("/auction/index.html");
  const status = page.getByRole("status");
  await openTab(page, "sell");
  await expect(page.getByText(t("pick_item"))).toBeVisible();
  await page.getByRole("button", { name: /Iron Arrow/ }).click();
  await expect(page.getByLabel(t("count"))).toBeFocused();
  await page.getByRole("button", { name: "All 60" }).click();
  await page.getByLabel(t("start")).fill("20");
  await page.getByLabel(t("buyout")).fill("100");
  await page.getByRole("button", { name: "48 h" }).click();
  await expect(page.locator("[data-fee]")).toHaveText("Putting it up costs 2 gold. The house keeps 5% of the price it sells for.");
  await expectNoHorizontalOverflow(page, ".auction__panel");
  await screenshot(page, testInfo, "auction-sell");

  await page.getByRole("button", { name: t("post"), exact: true }).click();
  await expect(status).toHaveText("Iron Arrow (60) is up for 48 hours. Putting it up cost 2 gold.");
  await expect(page.locator("[data-purse]")).toHaveText("638 gold");
  await expect(page.getByRole("button", { name: /Iron Arrow/ })).toHaveCount(0);

  await openTab(page, "mine");
  const mine = page.getByRole("region", { name: t("selling") });
  await expect(mine.getByRole("listitem")).toHaveCount(2);
  await expect(page.getByRole("region", { name: t("bidding") }).getByRole("listitem")).toHaveCount(1);
  await screenshot(page, testInfo, "auction-mine");
  await page.getByRole("button", { name: "Take back Iron Arrow ×60" }).click();
  await expect(status).toHaveText("You took Iron Arrow (60) back. Collect it at any auctioneer.");
  await expect(mine.getByRole("listitem")).toHaveCount(1);
  expect(await readSent(page, "AuctionPost")).toEqual([["0000C3A1:BF800000:BF800000", 60, 20, 100, 48]]);
});

test("what waits is collected one parcel at a time or all at once", async ({ page }, testInfo) => {
  await page.goto("/auction/index.html");
  await openTab(page, "collect");
  const parcels = page.locator(".auction-collect li");
  await expect(parcels).toHaveCount(3);
  await screenshot(page, testInfo, "auction-collect");

  await page.getByRole("button", { name: "Collect Silver Longsword" }).click();
  await expect(page.getByRole("status")).toHaveText("You collected Silver Longsword.");
  await expect(parcels).toHaveCount(2);
  await page.getByRole("button", { name: t("collect_all") }).click();
  await expect(page.getByRole("status")).toHaveText("You collected 85 gold.");
  await expect(page.getByText(t("nothing_waits"))).toBeVisible();
  await expect(page.locator("[data-purse]")).toHaveText("867 gold");
  expect(await readSent(page, "AuctionCollect")).toEqual([[11], [0]]);
});

test("the window waits for the auctioneer and sends no second request meanwhile", async ({ page }) => {
  await page.clock.install();
  await page.goto("/auction/index.html");
  await page.clock.runFor(100);
  await expect(page.getByRole("region", { name: t("title") })).toBeVisible();
  await pauseClock(page);

  await lot(page, "Iron Dagger").click();
  await page.getByRole("button", { name: t("bid"), exact: true }).click();
  await expect(page.getByRole("status")).toHaveText(t("waiting"));
  await expect(page.getByRole("button", { name: t("bid"), exact: true })).toBeDisabled();
  await expect(page.getByRole("button", { name: "Buy now for 60 gold" })).toBeDisabled();
  await page.clock.runFor(100);
  await expect(page.getByRole("status")).toHaveText("You bid 20 gold on Iron Dagger. The house keeps the gold until someone outbids you or you win.");
  expect(await readSent(page, "AuctionBid")).toEqual([[1, 20]]);
});

test("a long list scrolls and turns its pages", async ({ page }, testInfo) => {
  await page.goto("/auction/index.html#many");
  const list = page.locator(".auction-browse__lots .auction__list");
  await expect(list.locator("li")).toHaveCount(20);
  const scrolls = await list.evaluate((element) => {
    element.scrollTop = element.scrollHeight;
    return element.scrollHeight > element.clientHeight && element.scrollTop > 0;
  });
  expect(scrolls).toBe(true);
  await screenshot(page, testInfo, "auction-many");

  await page.getByRole("button", { name: t("next") }).click();
  await expect(list.locator("li")).toHaveCount(6);
  await expect(page.getByText("Page 2")).toBeVisible();
  await expect(page.getByRole("button", { name: t("next") })).toBeDisabled();
  expect(await readSent(page, "AuctionSearch")).toEqual([["", "All", 1]]);
});

test("long names and sums fit the window", async ({ page }, testInfo) => {
  await page.goto("/auction/index.html#long");
  await expect(page.locator("[data-purse]")).toHaveText("2,147,483,000 gold");
  await lot(page, "Ancient Silvermoon").click();
  await expectNoHorizontalOverflow(page, ".auction__panel");
  await screenshot(page, testInfo, "auction-long");
});

test("an empty house says so on every tab", async ({ page }, testInfo) => {
  await page.goto("/auction/index.html#empty");
  await expect(page.getByText(t("none"))).toBeVisible();
  await openTab(page, "sell");
  await expect(page.getByText(t("nothing_carried"))).toBeVisible();
  await openTab(page, "mine");
  await expect(page.getByText(t("selling_none"))).toBeVisible();
  await openTab(page, "collect");
  await expect(page.getByText(t("nothing_waits"))).toBeVisible();
  await screenshot(page, testInfo, "auction-empty");
});

test("Esc leaves the auction house", async ({ page }) => {
  await page.goto("/auction/index.html");
  await expect(page.getByRole("region", { name: t("title") })).toBeVisible();
  await page.keyboard.press("Escape");
  await expect(page.getByRole("region", { name: t("title") })).toHaveCount(0);
  expect(await readSent(page, "AuctionClose")).toEqual([[]]);
});
