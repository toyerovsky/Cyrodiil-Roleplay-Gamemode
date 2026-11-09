import { flushPromises } from "@vue/test-utils";
import { beforeEach, describe, expect, it } from "vitest";
import App from "../radial/App.vue";
import { NEXT, PREVIOUS } from "../radial/paging.js";
import { createBridge } from "../radial/bridge.js";
import { mountPage } from "./readym.js";

const SELF_KEY = "g";
const item = (id, label, reason = "") => ({ id, label, icon: "dice", disabled: reason !== "", reason });
const WHEEL = {
  title: "Lucia Gallus",
  items: [item("player.greet", "Greet"), item("player.trade", "Trade", "Lucia Gallus is busy with another trade."), item("player.pm", "Private message")],
};

describe("the wheel", () => {
  let game;
  let page;

  beforeEach(async () => {
    ({ game, page } = await mountPage(App, createBridge));
  });

  const show = () => game.emit("Wheel", JSON.stringify(WHEEL), SELF_KEY);
  const options = () => page.findAll("[role=option]");
  const press = async (key, init = {}) => {
    const event = new KeyboardEvent("keydown", { key, bubbles: true, cancelable: true, ...init });
    document.activeElement.dispatchEvent(event);
    await flushPromises();
    return event;
  };

  it("tells the game it is there once mounted, and shows nothing until a wheel comes", () => {
    expect(game.sent).toEqual([["WheelReady"]]);
    expect(page.find(".radial").exists()).toBe(false);
  });

  it("shows the wheel's options by name, the closed one marked, and takes the keyboard", async () => {
    await show();

    expect(options().map((o) => o.attributes("aria-label"))).toEqual(["Greet", "Trade", "Private message"]);
    expect(options()[1].attributes("aria-disabled")).toBe("true");
    expect(page.find(".rp-radial__title").text()).toBe("Lucia Gallus");
    expect(document.activeElement).toBe(page.get("[role=listbox]").element);
  });

  it("picks an option by its number", async () => {
    await show();
    await press("3");

    expect(game.readSent("WheelPick")).toEqual([["player.pm"]]);
    expect(page.find(".radial").exists()).toBe(false);
  });

  it("never picks a closed option, and says why it is closed", async () => {
    await show();
    await press("ArrowRight");
    await press("ArrowRight");
    expect(page.get(".rp-radial__reason").text()).toBe("Lucia Gallus is busy with another trade.");

    await press("Enter");
    await press("2");
    expect(game.readSent("WheelPick")).toEqual([]);
  });

  it("goes round with the arrows and picks with Enter", async () => {
    await show();
    await press("ArrowLeft");
    expect(page.get(".rp-radial__label").text()).toBe("Private message");

    await press("Enter");
    expect(game.readSent("WheelPick")).toEqual([["player.pm"]]);
  });

  it("closes on Escape and on the key that opened it, but not on that key held", async () => {
    await show();
    await press(SELF_KEY, { repeat: true });
    expect(page.find(".radial").exists()).toBe(true);

    await press(SELF_KEY);
    expect(game.readSent("WheelClose")).toEqual([[]]);

    await show();
    await press("Escape");
    expect(game.readSent("WheelClose")).toEqual([[], []]);
  });

  it("goes away when the game hides it", async () => {
    await show();
    await game.emit("Hidden");
    expect(page.find(".radial").exists()).toBe(false);
    expect(game.readSent("WheelClose")).toEqual([]);
  });
});

describe("a wheel of more than a page", () => {
  let game;
  let page;

  beforeEach(async () => {
    ({ game, page } = await mountPage(App, createBridge));
  });

  const many = (count) => Array.from({ length: count }, (_, index) => item(`emote:${index + 1}`, `Emote ${index + 1}`));
  const show = (count = 19) => game.emit("Wheel", JSON.stringify({ title: "Emotes", items: many(count) }), SELF_KEY);
  const labels = () => page.findAll("[role=option]").map((o) => o.attributes("aria-label"));
  const numbers = () => page.findAll(".rp-radial__number").map((n) => n.text());
  const press = async (key) => {
    document.activeElement.dispatchEvent(new KeyboardEvent("keydown", { key, bubbles: true, cancelable: true }));
    await flushPromises();
  };

  it("shows eight between Next first and Previous last, numbered 1 to 8, and which page it is on", async () => {
    await show();

    expect(labels()).toEqual(["Next page", ...many(8).map((i) => i.label), "Previous page"]);
    expect(numbers()).toEqual(["E", "1", "2", "3", "4", "5", "6", "7", "8", "Q"]);
    expect(page.get(".rp-radial__caption").text()).toBe("Page 1 of 3");
  });

  it("turns the pages with E and Q, round from the last to the first, and the last holds what is left", async () => {
    await show();
    await press("e");
    await press("e");
    expect(labels().slice(1, -1)).toEqual(["Emote 17", "Emote 18", "Emote 19"]);
    expect(page.get(".rp-radial__caption").text()).toBe("Page 3 of 3");

    await press("e");
    expect(labels()[1]).toBe("Emote 1");
    await press("q");
    expect(labels()[1]).toBe("Emote 17");
  });

  it("turns the pages from their wedges and the mouse wheel, never sending them as a pick", async () => {
    await show();
    await page.getComponent({ name: "RpRadial" }).vm.$emit("select", NEXT);
    expect(labels()[1]).toBe("Emote 9");
    await page.getComponent({ name: "RpRadial" }).vm.$emit("select", PREVIOUS);
    expect(labels()[1]).toBe("Emote 1");

    await page.get(".radial").trigger("wheel", { deltaY: 100 });
    expect(labels()[1]).toBe("Emote 9");
    expect(game.readSent("WheelPick")).toEqual([]);
  });

  it("picks by number on the page shown", async () => {
    await show();
    await press("e");
    await press("2");

    expect(game.readSent("WheelPick")).toEqual([["emote:10"]]);
  });

  it("starts again on the first page when it opens again", async () => {
    await show();
    await press("e");
    await press("Escape");
    await show();

    expect(page.get(".rp-radial__caption").text()).toBe("Page 1 of 3");
  });

  it("stays one wheel without paging for eight or fewer", async () => {
    await show(8);

    expect(labels()).toEqual(many(8).map((i) => i.label));
    expect(page.find(".rp-radial__caption").exists()).toBe(false);
    await press("e");
    expect(labels()[0]).toBe("Emote 1");
  });
});
