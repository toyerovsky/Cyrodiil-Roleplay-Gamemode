import { flushPromises } from "@vue/test-utils";
import { beforeEach, describe, expect, it } from "vitest";
import App from "../fisher/App.vue";
import { createBridge } from "../fisher/bridge.js";
import en from "../../locales/en/ui.fisher.json";
import pl from "../../locales/pl/ui.fisher.json";
import { mountPage } from "./readym.js";

// jsdom lays nothing out, so it has nothing to scroll to.
Element.prototype.scrollIntoView = () => {};

const text = (key) => en[`ui.fisher.${key}`];
const CATCH = [
  { item: "fishing.perch", species: "Perch", name: "Silverside Perch", trophy: false, count: 5, price: 2 },
  { item: "fishing.pike.trophy", species: "Pike", name: "Trophy Pike", trophy: true, count: 1, price: 21 },
];
const SHOP = [
  { ware: "fishing.rod.sturdy", kind: "Rod", icon: "rod-sturdy", name: "Sturdy Fishing Rod", price: 120, weight: 3.5, reachCm: 1000, line: 1.25, count: null },
  { ware: "fishing.worms", kind: "Bait", icon: "worms", name: "Fishing Worms", price: 2, weight: 0.1, reachCm: null, line: null, count: 12 },
];
const REPAIRS = [
  { item: "fishing.rod.worn", icon: "rod", name: "Fishing Rod (Worn)", condition: "Worn", price: 4 },
  { item: "fishing.rod.master.broken", icon: "rod-master", name: "Master Fishing Rod (Broken)", condition: "Broken", price: 203 },
];
const state = (changes = {}) => JSON.stringify({ gold: 50, most: 20, catch: CATCH, shop: SHOP, repairs: REPAIRS, ...changes });

describe("the fisher's window", () => {
  let game;
  let page;

  beforeEach(async () => ({ game, page } = await mountPage(App, createBridge)));

  const press = async (key, keyCode = 0) => {
    document.activeElement.dispatchEvent(new KeyboardEvent("keydown", { key, keyCode, bubbles: true, cancelable: true }));
    await flushPromises();
  };
  const names = () => page.findAll(".fisher__row .fisher__name > span:first-child").map((name) => name.text());
  const picked = () => page.find(".fisher__detail-name").text();
  const button = (action) => page.find(`[data-action="${action}"]`);
  const openTab = async (id) => {
    await page.findAll('[role="tab"]').find((tab) => tab.attributes("data-tab") === id).trigger("click");
    await flushPromises();
  };

  it("tells the game it is there, and shows nothing until a fisher opens", () => {
    expect(game.sent).toEqual([["FisherReady"]]);
    expect(page.find(".fisher").exists()).toBe(false);
  });

  it("opens on the catch: each fish with its picture, count and price, the purse, the first picked", async () => {
    await game.emit("State", state());

    expect(page.find(".rp-panel__title").text()).toBe(text("title"));
    expect(page.find("[data-purse]").text()).toBe("50 gold");
    expect(names()).toEqual(["Silverside Perch", "Trophy Pike"]);
    expect(page.findAll(".fisher__count").map((count) => count.text())).toEqual(["×5", "×1"]);
    expect(page.findAll(".fisher__row .fisher__price").map((price) => price.text())).toEqual(["2 gold", "21 gold"]);
    expect(page.find(".fisher__row img").attributes("src")).toContain("perch");
    expect(page.find('[data-row="fishing.pike.trophy"] .fisher__tag').text()).toBe(text("trophy"));
    expect(picked()).toBe("Silverside Perch");
    expect(document.activeElement).toBe(page.find('[role="listbox"]').element);
  });

  it("sells one with Enter, and waits for the fisher before another", async () => {
    await game.emit("State", state());
    await press("Enter");
    await press("Enter");
    expect(game.readSent("FisherSell")).toEqual([["fishing.perch", 1]]);
    expect(page.find('[role="status"]').text()).toBe(text("waiting"));

    await game.emit("Answer", "Sold Silverside Perch for 2 gold.", true);
    expect(page.find('[role="status"]').text()).toBe("Sold Silverside Perch for 2 gold.");
    expect(page.find(".fisher__status--done").exists()).toBe(true);
  });

  it("sells all of one fish, or the whole catch at what it comes to", async () => {
    await game.emit("State", state());
    expect(button("sell-all-of").text()).toBe("Sell all 5 for 10 gold");
    await button("sell-all-of").trigger("click");
    expect(game.readSent("FisherSell")).toEqual([["fishing.perch", 5]]);
    await game.emit("Answer", "Sold.", true);

    expect(button("sell-everything").text()).toBe("Sell the whole catch for 31 gold");
    await button("sell-everything").trigger("click");
    expect(game.readSent("FisherSellAll")).toEqual([[]]);
  });

  it("sells no more of a kind at once than the fisher takes in one deal", async () => {
    await game.emit("State", state({ catch: [{ ...CATCH[0], count: 25 }] }));

    expect(button("sell-all-of").text()).toBe("Sell 20 for 40 gold");
    await button("sell-all-of").trigger("click");
    expect(game.readSent("FisherSell")).toEqual([["fishing.perch", 20]]);
  });

  it("offers no sale of all for a single fish", async () => {
    await game.emit("State", state());
    await press("ArrowDown");

    expect(picked()).toBe("Trophy Pike");
    expect(button("sell-all-of").exists()).toBe(false);
  });

  it("picks the next fish when the one picked was sold out", async () => {
    await game.emit("State", state());
    await game.emit("State", state({ catch: [CATCH[1]] }));

    expect(picked()).toBe("Trophy Pike");
  });

  it("turns the tabs with Q and E, as the game hands a page its keys", async () => {
    await game.emit("State", state());
    await press("", 69);
    expect(page.find('[role="tab"][aria-selected="true"]').text()).toBe(text("tab.shop"));
    expect(document.activeElement).toBe(page.find('[role="listbox"]').element);
    expect(picked()).toBe("Sturdy Fishing Rod");

    await press("", 81);
    await press("", 81);
    expect(page.find('[role="tab"][aria-selected="true"]').text()).toBe(text("tab.records"));
    await press("", 81);
    expect(page.find('[role="tab"][aria-selected="true"]').text()).toBe(text("tab.repair"));
  });

  it("shows a rod's reach and line, and buys one at a time", async () => {
    await game.emit("State", state({ gold: 200 }));
    await openTab("shop");

    expect(page.find("[data-rod-facts]").text()).toBe("casts 10 m · line strength 125%");
    expect(page.find("[data-carried]").exists()).toBe(false);
    expect(page.find(".fisher__count-pick").exists()).toBe(false);
    await page.find('[role="listbox"]').element.focus();
    await press("Enter");
    expect(game.readSent("FisherBuy")).toEqual([["fishing.rod.sturdy", 1]]);
  });

  it("counts baits as the trader does, up to what the purse pays for", async () => {
    await game.emit("State", state({ gold: 7 }));
    await openTab("shop");
    await page.find('[role="listbox"]').element.focus();
    await press("ArrowDown");
    expect(picked()).toBe("Fishing Worms");
    expect(page.find("[data-carried]").text()).toBe("You carry 12");
    expect(page.find("[data-rod-facts]").exists()).toBe(false);

    for (let i = 0; i < 5; i++) await press("+");
    expect(page.find(".fisher__amount").text()).toBe("3");
    expect(page.find(".fisher__total").text()).toBe("Total 6 gold");
    await press("-");
    await press("Enter");
    expect(game.readSent("FisherBuy")).toEqual([["fishing.worms", 2]]);
  });

  it("marks what the purse cannot pay for and will not buy it", async () => {
    await game.emit("State", state({ gold: 50 }));
    await openTab("shop");

    expect(page.find('[data-row="fishing.rod.sturdy"]').classes()).toContain("fisher__row--dear");
    expect(button("buy").text()).toBe(text("too_poor"));
    expect(button("buy").attributes("disabled")).toBeDefined();
    await page.find('[role="listbox"]').element.focus();
    await press("Enter");
    expect(game.readSent("FisherBuy")).toEqual([]);
  });

  it("mends a rod for its price, and not one the purse cannot pay for", async () => {
    await game.emit("State", state());
    await openTab("repair");

    expect(page.findAll(".fisher__tag").map((tag) => tag.text())).toEqual(["Worn", "Broken"]);
    expect(page.find(".fisher__detail").text()).toContain("Mended as good as new for 4 gold.");
    await button("repair").trigger("click");
    expect(game.readSent("FisherRepair")).toEqual([["fishing.rod.worn"]]);
    await game.emit("Answer", "Mended.", true);

    await page.find('[data-row="fishing.rod.master.broken"]').trigger("click");
    expect(button("repair").text()).toBe(text("too_poor"));
    expect(button("repair").attributes("disabled")).toBeDefined();
  });

  it("says so in each tab when there is nothing in it, the tabs keeping the keys", async () => {
    await game.emit("State", state({ catch: [], shop: [], repairs: [] }));
    expect(page.find("[data-empty]").text()).toBe(text("empty.catch"));
    expect(page.find('[role="status"]').text()).toBe(text("hint.empty"));
    expect(button("sell-everything").exists()).toBe(false);
    expect(document.activeElement.getAttribute("role")).toBe("tab");

    await openTab("shop");
    expect(page.find("[data-empty]").text()).toBe(text("empty.shop"));
    await openTab("repair");
    expect(page.find("[data-empty]").text()).toBe(text("empty.repair"));
  });

  it("leaves with Escape and closes when the game says so", async () => {
    await game.emit("State", state());
    await press("Escape");
    expect(game.readSent("FisherClose")).toEqual([[]]);

    await game.emit("Closed");
    expect(page.find(".fisher").exists()).toBe(false);
  });

  it("speaks Polish when the game says so", async () => {
    await game.emit("Locale", "pl");
    await game.emit("State", state());

    expect(page.find(".rp-panel__title").text()).toBe(pl["ui.fisher.title"]);
    expect(page.find("[data-purse]").text()).toBe("50 szt. złota");
  });
});

describe("the fisher's records", () => {
  const DAY = Date.UTC(2026, 10, 20, 12);
  const landed = (rank, name, lengthCm, grams, mine = false) => ({ rank, name, lengthCm, grams, at: DAY, mine });
  const BOARD = {
    species: [
      {
        species: "Perch",
        name: "Silverside Perch",
        top: [landed(1, "Jauffre Valtieri", 35, 549), landed(2, "Marius Caro", 34, 503, true), landed(2, "Nord", 34, 503)],
        own: null,
      },
      { species: "Betty", name: "River Betty", top: [], own: null },
      {
        species: "Pike",
        name: "Pike",
        top: [landed(1, "Bosmer", 108, 10456), landed(2, "Stranger", 101, 8552), landed(3, "Dar-Ma", 97, 1000), landed(4, "Orc", 95, 999), landed(5, "Nord", 90, 6051)],
        own: landed(1204, "Marius Caro", 74, 3363, true),
      },
    ],
  };

  let game;
  let page;

  beforeEach(async () => {
    ({ game, page } = await mountPage(App, createBridge));
    await game.emit("State", state());
  });

  const press = async (key, keyCode = 0) => {
    document.activeElement.dispatchEvent(new KeyboardEvent("keydown", { key, keyCode, bubbles: true, cancelable: true }));
    await flushPromises();
  };
  const openTab = async (id) => {
    await page.findAll('[role="tab"]').find((tab) => tab.attributes("data-tab") === id).trigger("click");
    await flushPromises();
  };
  const openRecords = async (board = BOARD) => {
    await openTab("records");
    await game.emit("Records", JSON.stringify(board));
  };
  const cells = (row) => row.findAll("td").map((cell) => cell.text().replace(/\s+/g, " "));
  const listed = () => page.findAll("[data-records] tbody tr").map(cells);

  it("asks for the board once as the tab opens, and says the fisher is looking meanwhile", async () => {
    expect(game.readSent("FisherAskRecords")).toEqual([]);
    await openTab("records");

    expect(game.readSent("FisherAskRecords")).toEqual([[]]);
    expect(page.find("[data-empty]").text()).toBe(text("empty.records"));
    expect(page.find('[role="status"]').text()).toBe(text("hint.empty"));
  });

  it("lists every species with its longest fish, and the picked one's fish by rank with size and day", async () => {
    await openRecords();

    expect(page.findAll(".fisher__row .fisher__name").map((name) => name.text())).toEqual(["Silverside Perch", "River Betty", "Pike"]);
    expect(page.findAll(".fisher__best").map((best) => best.text())).toEqual(["35 cm", "", "108 cm"]);
    expect(page.find(".fisher__row img").attributes("src")).toContain("perch");
    expect(page.find(".fisher__detail-name").text()).toBe("Silverside Perch");
    expect(page.findAll("[data-records] th").map((column) => column.text())).toEqual(
      ["rank", "name", "length", "weight", "date"].map((column) => text(`records.column.${column}`)),
    );
    expect(listed()).toEqual([
      ["1.", "Jauffre Valtieri", "35 cm", "549 g", "Nov 20, 2026"],
      ["2.", `Marius Caro ${text("records.you")}`, "34 cm", "503 g", "Nov 20, 2026"],
      ["2.", "Nord", "34 cm", "503 g", "Nov 20, 2026"],
    ]);
    expect(page.find('[role="status"]').text()).toBe(text("hint.records"));
  });

  it("marks the reader's own fish on the board, and shows no second line for it", async () => {
    await openRecords();

    expect(page.findAll("[data-records] tbody tr").map((row) => row.attributes("data-mine"))).toEqual([undefined, "true", undefined]);
    expect(page.find("[data-own]").exists()).toBe(false);
  });

  it("shows the reader's best under the five with its rank when it is not among them", async () => {
    await openRecords();
    await page.find('[data-row="Pike"]').trigger("click");

    expect(listed()).toHaveLength(5);
    expect(page.findAll("[data-records] tbody tr[data-mine]")).toHaveLength(0);
    expect(cells(page.find("[data-own]"))).toEqual(["1,204.", text("records.own"), "74 cm", "3.4 kg", "Nov 20, 2026"]);
  });

  it("writes a weight in grams under a kilogram and in kilograms to one decimal from there", async () => {
    await openRecords();
    await page.find('[data-row="Pike"]').trigger("click");

    expect(listed().map((row) => row[3])).toEqual(["10.5 kg", "8.6 kg", "1 kg", "999 g", "6.1 kg"]);
  });

  it("says so in one line for a species nobody has caught", async () => {
    await openRecords();
    await page.find('[data-row="Betty"]').trigger("click");

    expect(page.find("[data-no-records]").text()).toBe(text("records.none"));
    expect(page.find("[data-records]").exists()).toBe(false);
    expect(page.find('[data-row="Betty"]').classes()).toContain("fisher__row--none");
  });

  it("hands the arrows to the species once the board comes, when the tab was reached from the keys", async () => {
    await press("", 81);
    expect(page.find('[role="tab"][aria-selected="true"]').text()).toBe(text("tab.records"));
    await game.emit("Records", JSON.stringify(BOARD));

    expect(document.activeElement).toBe(page.find('[role="listbox"]').element);
    await press("End");
    expect(page.find(".fisher__detail-name").text()).toBe("Pike");
    await press("Enter");
    expect(game.sent.map(([name]) => name)).toEqual(["FisherReady", "FisherAskRecords"]);
  });

  it("says the board cannot be read when the fisher refuses it", async () => {
    await openTab("records");
    await game.emit("Answer", "The fisher cannot find the book of records just now.", false);

    expect(page.find("[data-empty]").text()).toBe(text("records.unread"));
    expect(page.find('[role="status"]').text()).toBe("The fisher cannot find the book of records just now.");
    expect(page.find(".fisher__status--refused").exists()).toBe(true);
  });

  it("asks again at each visit to the tab and shows the board it has until the new one comes", async () => {
    await openRecords();
    await openTab("catch");
    await openTab("records");

    expect(game.readSent("FisherAskRecords")).toEqual([[], []]);
    expect(listed()).toHaveLength(3);
    await game.emit("Records", JSON.stringify({ species: [{ ...BOARD.species[0], top: [landed(1, "Dar-Ma", 36, 597)] }] }));
    expect(listed()).toEqual([["1.", "Dar-Ma", "36 cm", "597 g", "Nov 20, 2026"]]);
  });

  it("does not ask twice while the fisher is still looking", async () => {
    await openTab("records");
    await openTab("catch");
    await openTab("records");

    expect(game.readSent("FisherAskRecords")).toEqual([[]]);
  });

  it("forgets the board when the window closes", async () => {
    await openRecords();
    await game.emit("Closed");
    await game.emit("State", state());
    await openTab("records");

    expect(page.find("[data-empty]").text()).toBe(text("empty.records"));
    expect(game.readSent("FisherAskRecords")).toEqual([[], []]);
  });

  it("writes the numbers and the day as Polish does", async () => {
    await game.emit("Locale", "pl");
    await openRecords();
    await page.find('[data-row="Pike"]').trigger("click");

    expect(listed()[0]).toEqual(["1.", "Bosmer", "108 cm", "10,5 kg", "20 lis 2026"]);
    expect(cells(page.find("[data-own]"))[1]).toBe(pl["ui.fisher.records.own"]);
  });
});

describe("the fisher's texts", () => {
  it("are the same keys in English and in Polish", () => {
    expect(Object.keys(pl).sort()).toEqual(Object.keys(en).sort());
  });
});
