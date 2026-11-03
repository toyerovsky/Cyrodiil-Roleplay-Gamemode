import { flushPromises } from "@vue/test-utils";
import { beforeEach, describe, expect, it } from "vitest";
import App from "../radial/App.vue";
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
