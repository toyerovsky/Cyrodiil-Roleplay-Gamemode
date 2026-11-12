import { flushPromises } from "@vue/test-utils";
import { beforeEach, describe, expect, it } from "vitest";
import App from "../stable/App.vue";
import { createBridge } from "../stable/bridge.js";
import { mountPage } from "./readym.js";

const PRICES = { Paint: 500, Bay: 1000, Chestnut: 2500, White: 4000, Black: 5000 };

const state = ({ owned = null, gold = 1340 } = {}) =>
  JSON.stringify({ horses: Object.entries(PRICES).map(([id, price]) => ({ id, price, owned: id === owned })), gold });

describe("the stable's window", () => {
  let game;
  let page;

  beforeEach(async () => ({ game, page } = await mountPage(App, createBridge)));

  const row = (id) => page.find(`[data-horse="${id}"]`);
  const button = (id) => row(id).find("button");
  const readSum = (name) => page.find(`[data-sum="${name}"] dd`).text();

  it("tells the game it is there, and shows nothing until a stablemaster opens it", () => {
    expect(game.sent).toEqual([["StableReady"]]);
    expect(page.find(".stable").exists()).toBe(false);
  });

  it("lists the game's five horses with their prices, the gold carried and no horse yet", async () => {
    await game.emit("State", state());
    expect(page.findAll("[data-horse]").map((each) => each.find(".stable__name").text())).toEqual([
      "Paint horse",
      "Bay horse",
      "Chestnut horse",
      "White horse",
      "Black horse",
    ]);
    expect(row("Chestnut").find(".stable__price").text()).toBe("2,500 gold");
    expect(readSum("horse")).toBe("None yet");
    expect(readSum("gold")).toBe("1,340 gold");
    expect(page.find(".stable__note").exists()).toBe(false);
    expect(document.activeElement).toBe(button("Paint").element);
  });

  it("offers only the horses the gold carried pays for", async () => {
    await game.emit("State", state());
    expect(button("Bay").element.disabled).toBe(false);
    expect(button("Chestnut").element.disabled).toBe(true);
    expect(button("Chestnut").text()).toBe("Not enough gold");
    expect(button("Chestnut").attributes("aria-label")).toBe("Not enough gold: Chestnut horse");
  });

  it("buys a first horse at once and waits for the stablemaster", async () => {
    await game.emit("State", state());
    await button("Bay").trigger("click");
    expect(game.readSent("StableBuy")).toEqual([["Bay"]]);
    expect(page.find(".stable__status").text()).toBe("The stablemaster saddles your horse...");
    expect(button("Paint").element.disabled).toBe(true);

    await game.emit("Answer", "You bought a Bay horse for 1000 gold.", true);
    await game.emit("State", state({ owned: "Bay", gold: 340 }));
    expect(page.find(".stable__status--bought").text()).toBe("You bought a Bay horse for 1000 gold.");
    expect(readSum("horse")).toBe("Bay horse");
    expect(button("Bay").text()).toBe("Yours");
    expect(button("Bay").element.disabled).toBe(true);
    // Nothing else is within the purse now, so the keys go on from Leave.
    expect(document.activeElement).toBe(page.find('[data-action="close"]').element);
  });

  it("says before a purchase that the horse owned goes, and asks first", async () => {
    await game.emit("State", state({ owned: "Bay", gold: 6200 }));
    expect(page.find(".stable__note").text()).toBe("You keep one horse. Buying another replaces your Bay horse, and nothing is paid back for it.");

    await button("Black").trigger("click");
    await flushPromises();
    expect(game.readSent("StableBuy")).toEqual([]);
    const ask = page.find('[role="alertdialog"]');
    expect(ask.text()).toContain("Buy the Black horse for 5,000 gold in place of your Bay horse?");
    expect(document.activeElement).toBe(page.find('[data-action="confirm"]').element);

    await page.find('[data-action="confirm"]').trigger("click");
    expect(game.readSent("StableBuy")).toEqual([["Black"]]);
    expect(page.find('[role="alertdialog"]').exists()).toBe(false);
  });

  it("keeps the horse owned when the player says no, on the button or with Escape", async () => {
    await game.emit("State", state({ owned: "Bay", gold: 6200 }));
    await button("White").trigger("click");
    await page.find('[data-action="cancel"]').trigger("click");
    await flushPromises();
    expect(page.find('[role="alertdialog"]').exists()).toBe(false);
    expect(document.activeElement).toBe(button("White").element);

    await button("Paint").trigger("click");
    window.dispatchEvent(new KeyboardEvent("keydown", { key: "Escape", cancelable: true }));
    await flushPromises();
    expect(page.find('[role="alertdialog"]').exists()).toBe(false);
    expect(page.find(".stable").exists()).toBe(true);
    expect(game.readSent("StableBuy")).toEqual([]);
    expect(game.readSent("StableClose")).toEqual([]);
  });

  it("drops the question when the gold carried no longer pays for the horse", async () => {
    await game.emit("State", state({ owned: "Bay", gold: 6200 }));
    await button("Black").trigger("click");
    await game.emit("State", state({ owned: "Bay", gold: 4500 }));
    expect(page.find('[role="alertdialog"]').exists()).toBe(false);
    expect(button("Black").element.disabled).toBe(true);
  });

  it("buys one horse at a time, the next only after the stablemaster answered", async () => {
    await game.emit("State", state({ gold: 9000 }));
    await button("Paint").trigger("click");
    await button("Bay").trigger("click");
    expect(game.readSent("StableBuy")).toEqual([["Paint"]]);

    await game.emit("Answer", "That costs more than you carry.", false);
    expect(page.find(".stable__status--refused").text()).toBe("That costs more than you carry.");
    expect(button("Bay").element.disabled).toBe(false);
  });

  it("leaves on Escape and closes when the server says so", async () => {
    await game.emit("State", state());
    window.dispatchEvent(new KeyboardEvent("keydown", { key: "Escape", cancelable: true }));
    expect(game.readSent("StableClose")).toEqual([[]]);
    await game.emit("Closed");
    expect(page.find(".stable").exists()).toBe(false);
  });

  it("opens again clean after it was closed with a question on it", async () => {
    await game.emit("State", state({ owned: "Bay", gold: 6200 }));
    await button("White").trigger("click");
    await game.emit("Closed");
    await game.emit("State", state({ owned: "Bay", gold: 6200 }));
    expect(page.find('[role="alertdialog"]').exists()).toBe(false);
    expect(button("White").element.disabled).toBe(false);
  });

  it("reads in Polish to a Polish player", async () => {
    await game.emit("Locale", "pl");
    await game.emit("State", state({ owned: "Paint" }));
    expect(row("Black").find(".stable__name").text()).toBe("Koń kary");
    expect(readSum("horse")).toBe("Koń srokaty");
    expect(readSum("gold")).toBe("1340 szt. złota");
    expect(button("Bay").text()).toBe("Kup");
  });
});
