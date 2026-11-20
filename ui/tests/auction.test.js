import { beforeEach, describe, expect, it } from "vitest";
import App from "../auction/App.vue";
import { createBridge } from "../auction/bridge.js";
import { describeLeft, findFee, readSum } from "../auction/lots.js";
import { pickTexts } from "../shared/locale.js";
import { mountPage } from "./readym.js";

const t = pickTexts("en", "ui.auction.");
const TERMS = { feePercent: 2, cutPercent: 5, hours: [12, 24, 48], maxLots: 10, maxPrice: 100000 };
const lot = (changes = {}) => ({ id: 7, name: "Iron Dagger", count: 1, used: false, category: "Weapons", startPrice: 20, buyoutPrice: 60,
  topBid: null, bids: 0, minimumBid: 20, endsInMs: 3_600_000, mine: false, leading: false, ...changes });
const state = (changes = {}) => JSON.stringify({
  gold: 100,
  terms: TERMS,
  items: [{ key: "dagger", name: "Iron Dagger", used: false, count: 2 }],
  browse: { text: "", category: "All", page: 0, more: false, lots: [lot()] },
  selling: [],
  bidding: [],
  parcels: [],
  ...changes,
});

describe("the auction house's window", () => {
  let game;
  let page;

  beforeEach(async () => ({ game, page } = await mountPage(App, createBridge)));

  const tab = (id) => page.find(`[data-tab="${id}"]`).trigger("click");
  const click = (selector) => page.find(selector).trigger("click");
  const status = () => page.find(".auction__status").text();

  it("tells the game it is there, and shows nothing until an auctioneer opens it", () => {
    expect(game.sent).toEqual([["AuctionReady"]]);
    expect(page.find(".auction").exists()).toBe(false);
  });

  it("opens on the lots with the search field ready, the purse and no seller anywhere", async () => {
    await game.emit("State", state());
    expect(page.find("[data-purse]").text()).toBe("100 gold");
    expect(document.activeElement).toBe(page.find('.auction-browse input').element);
    expect(page.find('[data-lot="7"]').text()).toContain("Iron Dagger");
    expect(page.find('[data-lot="7"]').text()).toContain("1 h 0 min left");
  });

  it("searches by the name typed and by the kind picked, from the first page", async () => {
    await game.emit("State", state());
    await page.find(".auction-browse input").setValue(" dagger ");
    await page.find(".auction-browse__search").trigger("submit");
    await click('[data-category="Weapons"]');
    expect(game.readSent("AuctionSearch")).toEqual([["dagger", "All", 0], ["dagger", "Weapons", 0]]);
    expect(status()).toBe(t.searching);
  });

  it("keeps the kind just picked for a search sent before the house answered", async () => {
    await game.emit("State", state());
    await click('[data-category="Potions"]');
    await page.find(".auction-browse input").setValue("healing");
    await page.find(".auction-browse__search").trigger("submit");
    expect(game.readSent("AuctionSearch")).toEqual([["", "Potions", 0], ["healing", "Potions", 0]]);
    expect(page.find('[data-category="Potions"]').attributes("aria-pressed")).toBe("true");
  });

  it("bids the least the lot takes unless the player types more, and waits for the house", async () => {
    await game.emit("State", state());
    await click('[data-lot="7"]');
    expect(page.find('[data-field="bid"]').element.value).toBe("20");
    await page.find('[data-field="bid"]').setValue("25");
    await page.find(".auction-detail__bid").trigger("submit");
    expect(game.readSent("AuctionBid")).toEqual([[7, 25]]);
    expect(status()).toBe(t.waiting);
    expect(page.find('[data-action="bid"]').element.disabled).toBe(true);

    await game.emit("Answer", "You bid 25 gold on Iron Dagger.", true);
    expect(status()).toBe("You bid 25 gold on Iron Dagger.");
    expect(page.find('[data-action="bid"]').element.disabled).toBe(false);
  });

  it("marks a bid the house would refuse at its field and sends nothing", async () => {
    await game.emit("State", state());
    await click('[data-lot="7"]');
    for (const [typed, problem] of [["19", "Bid a whole sum from 20 to 100,000 gold."], ["60", "At 60 gold, buy it now instead."], ["ten", "Bid a whole sum from 20 to 100,000 gold."]]) {
      await page.find('[data-field="bid"]').setValue(typed);
      await page.find(".auction-detail__bid").trigger("submit");
      expect(page.find('[role="alert"]').text()).toBe(problem);
      expect(page.find('[data-field="bid"]').attributes("aria-invalid")).toBe("true");
    }
    expect(game.readSent("AuctionBid")).toEqual([]);
  });

  it("buys now at the price shown, and refuses it to a purse too light", async () => {
    await game.emit("State", state());
    await click('[data-lot="7"]');
    await click('[data-action="buy"]');
    expect(game.readSent("AuctionBuy")).toEqual([[7, 60]]);

    await game.emit("Answer", "Bought.", true);
    await game.emit("State", state({ gold: 10 }));
    await click('[data-action="buy"]');
    expect(page.find('[role="alert"]').text()).toBe("You carry only 10 gold.");
    expect(game.readSent("AuctionBuy")).toEqual([[7, 60]]);
  });

  it("offers only the purchase once the next bid would reach the buy now price", async () => {
    await game.emit("State", state({ browse: { text: "", category: "All", page: 0, more: false, lots: [lot({ topBid: 58, minimumBid: 60 })] } }));
    await click('[data-lot="7"]');
    expect(page.find('[data-action="bid"]').exists()).toBe(false);
    expect(page.find('[data-action="buy"]').exists()).toBe(true);
  });

  it("offers neither a bid nor a purchase on the player's own lot", async () => {
    await game.emit("State", state({ browse: { text: "", category: "All", page: 0, more: false, lots: [lot({ mine: true })] } }));
    await click('[data-lot="7"]');
    expect(page.find('[data-action="bid"]').exists()).toBe(false);
    expect(page.find('[data-action="buy"]').exists()).toBe(false);
    expect(page.find(".auction-detail__note").text()).toBe(t.own_lot);
  });

  it("puts up what the player carries with the fee shown, and checks the lot as the house does", async () => {
    await game.emit("State", state());
    await tab("sell");
    await click('[data-item="dagger"]');
    await page.find('[data-field="start"]').setValue("50");
    await page.find('[data-field="buyout"]').setValue("40");
    await page.find(".auction-sell__form").trigger("submit");
    expect(page.find('[role="alert"]').text()).toBe(t.buyout_low);
    expect(page.find('[data-field="buyout"]').attributes("aria-invalid")).toBe("true");

    await page.find('[data-field="buyout"]').setValue("");
    await click('[data-fill="all"]');
    await click('[data-hours="48"]');
    expect(page.find("[data-fee]").text()).toBe("Putting it up costs 1 gold. The house keeps 5% of the price it sells for.");
    await page.find(".auction-sell__form").trigger("submit");
    expect(game.readSent("AuctionPost")).toEqual([["dagger", 2, 50, 0, 48]]);
  });

  it("asks for at least one price", async () => {
    await game.emit("State", state());
    await tab("sell");
    await click('[data-item="dagger"]');
    await page.find(".auction-sell__form").trigger("submit");
    expect(page.find('[role="alert"]').text()).toBe(t.price_missing);
    expect(game.readSent("AuctionPost")).toEqual([]);
  });

  it("takes back a lot nobody bid on, and leaves one bid on to run", async () => {
    await game.emit("State", state({ selling: [lot({ mine: true }), lot({ id: 8, mine: true, bids: 1, topBid: 25 })] }));
    await tab("mine");
    expect(page.find('[data-lot="8"]').text()).toContain(t.has_bids);
    expect(page.find('[data-lot="8"] [data-action="withdraw"]').exists()).toBe(false);
    await click('[data-lot="7"] [data-action="withdraw"]');
    expect(game.readSent("AuctionWithdraw")).toEqual([[7]]);
  });

  it("collects one parcel or every one, and names what waits on the tab", async () => {
    const parcels = [{ id: 3, reason: "Sale", gold: 57, name: "Iron Dagger", count: 1, used: false },
      { id: 4, reason: "Won", gold: 0, name: "Steel Arrow", count: 40, used: false }];
    await game.emit("State", state({ parcels }));
    expect(page.find('[data-tab="collect"]').text()).toBe("Collect (2)");
    await tab("collect");
    expect(page.find('[data-parcel="3"]').text()).toContain("57 gold");
    expect(page.find('[data-parcel="4"]').text()).toContain("Steel Arrow ×40");
    await click('[data-parcel="4"] [data-action="collect"]');
    await game.emit("Answer", "Collected.", true);
    await click('[data-action="collect-all"]');
    expect(game.readSent("AuctionCollect")).toEqual([[4], [0]]);
  });

  it("leaves on Esc and takes a held key as one press", async () => {
    await game.emit("State", state());
    window.dispatchEvent(new KeyboardEvent("keydown", { key: "Escape", repeat: true, bubbles: true, cancelable: true }));
    expect(game.readSent("AuctionClose")).toEqual([]);
    window.dispatchEvent(new KeyboardEvent("keydown", { key: "Escape", bubbles: true, cancelable: true }));
    expect(game.readSent("AuctionClose")).toEqual([[]]);
    await game.emit("Closed");
    expect(page.find(".auction").exists()).toBe(false);
  });
});

describe("the house's arithmetic on the page", () => {
  it("rounds the listing fee up as the server does, at least 1 gold when there is one", () => {
    expect([findFee(100, 2), findFee(101, 2), findFee(10, 2), findFee(1000, 0)]).toEqual([2, 3, 1, 0]);
  });

  it("reads an empty field as no price and anything not whole as nothing", () => {
    expect([readSum(""), readSum(" 40 "), readSum("4.5"), readSum("-1")]).toEqual([0, 40, null, null]);
  });

  it("counts the time left in whole minutes, rounded up", () => {
    expect([describeLeft(t, 0), describeLeft(t, 1), describeLeft(t, 59 * 60_000), describeLeft(t, 125 * 60_000)])
      .toEqual(["Ending", "1 min left", "59 min left", "2 h 5 min left"]);
  });
});
