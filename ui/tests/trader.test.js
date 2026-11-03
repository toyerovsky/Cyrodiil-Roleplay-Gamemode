import { flushPromises } from "@vue/test-utils";
import { beforeEach, describe, expect, it } from "vitest";
import App from "../trader/App.vue";
import { createBridge } from "../trader/bridge.js";
import { WARE_ICONS } from "../trader/wares.js";
import { mountPage } from "./readym.js";

// jsdom lays nothing out, so it has nothing to scroll to.
Element.prototype.scrollIntoView = () => {};

const state = (changes = {}) =>
  JSON.stringify({
    shop: "general",
    gold: 20,
    most: 20,
    wares: [
      { id: "Breadloaf", category: "food", price: 2, weight: 0.5 },
      { id: "WeapIronLongsword", category: "weapons", price: 28, weight: 20 },
      { id: "Lockpick", category: "tools", price: 5, weight: 0 },
    ],
    ...changes,
  });

describe("the trader's window", () => {
  let game;
  let page;

  beforeEach(async () => ({ game, page } = await mountPage(App, createBridge)));

  const press = async (key) => {
    document.activeElement.dispatchEvent(new KeyboardEvent("keydown", { key, bubbles: true, cancelable: true }));
    await flushPromises();
  };

  it("tells the game it is there, and shows nothing until a trader opens", () => {
    expect(game.sent).toEqual([["TraderReady"]]);
    expect(page.find(".trader").exists()).toBe(false);
  });

  it("names the wares from the game's own names, with the purse and the first ware picked", async () => {
    await game.emit("State", state());
    expect(page.find(".rp-panel__title").text()).toBe("General goods");
    expect(page.findAll(".trader__name").map((name) => name.text())).toEqual(["Bread Loaf", "Iron Longsword", "Lockpick"]);
    expect(page.find(".trader__purse").text()).toContain("20 gold");
    expect(page.find(".trader__detail-name").text()).toBe("Bread Loaf");
    expect(document.activeElement).toBe(page.find(".trader__wares").element);
  });

  it("buys so many of the picked ware and shows what it came to", async () => {
    await game.emit("State", state());
    await press("+");
    await press("+");
    expect(page.find(".trader__total").text()).toBe("Total 6 gold");
    await press("Enter");
    expect(game.readSent("TraderBuy")).toEqual([["Breadloaf", 3]]);

    await game.emit("Answer", "Paid 6 gold coins.", true);
    expect(page.find(".trader__status").text()).toBe("Bought Bread Loaf × 3. Paid 6 gold coins.");
    expect(page.find(".trader__status").classes()).toContain("trader__status--bought");
  });

  it("will not buy what the purse cannot pay, and marks what is too dear", async () => {
    await game.emit("State", state());
    await press("ArrowDown");
    expect(page.find(".trader__detail-name").text()).toBe("Iron Longsword");
    expect(page.find(".trader__buy").text()).toBe("Not enough gold");
    expect(page.find(".trader__buy").attributes("disabled")).toBeDefined();
    expect(page.find(".trader__ware--dear").text()).toContain("Iron Longsword");
    await press("Enter");
    expect(game.readSent("TraderBuy")).toEqual([]);
  });

  it("keeps how many to what the purse pays for", async () => {
    await game.emit("State", state({ gold: 7 }));
    for (let i = 0; i < 5; i++) await press("+");
    expect(page.find(".trader__amount").text()).toBe("3");
  });

  it("shows one kind of wares at a time, the kind picked pressed", async () => {
    await game.emit("State", state());
    const kinds = () => page.findAll(".trader__categories button");
    expect(kinds().map((kind) => kind.attributes("aria-pressed"))).toEqual(["true", "false", "false", "false"]);

    await kinds().find((button) => button.text() === "Tools").trigger("click");
    expect(page.findAll(".trader__name").map((name) => name.text())).toEqual(["Lockpick"]);
    expect(kinds().map((kind) => kind.attributes("aria-pressed"))).toEqual(["false", "false", "false", "true"]);
  });

  it("leaves with Escape and closes when the game says so", async () => {
    await game.emit("State", state());
    await press("Escape");
    expect(game.readSent("TraderClose")).toEqual([[]]);
    await game.emit("Closed");
    expect(page.find(".trader").exists()).toBe(false);
  });

  it("has an icon in the atlas for every ware a name is written for", async () => {
    const names = (await import("../../locales/en/ui.wares.json")).default;
    expect(Object.keys(names).map((key) => key.replace("ui.wares.", "")).sort()).toEqual(Object.keys(WARE_ICONS).sort());
  });
});
