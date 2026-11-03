import { flushPromises } from "@vue/test-utils";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import App from "../exchange/App.vue";
import { createBridge } from "../exchange/bridge.js";
import { mountPage } from "./readym.js";

const OFFER_AFTER_MS = 400;
const CONFIRM_DELAY_MS = 3000;

const state = (changes = {}) =>
  JSON.stringify({
    version: 1,
    other: "Lucia Gallus",
    carried: 120,
    max: 120,
    confirmInMs: 0,
    mine: { gold: 0, confirmed: false },
    theirs: { gold: 0, confirmed: false },
    ...changes,
  });

describe("the trade window", () => {
  let game;
  let page;

  beforeEach(async () => {
    vi.useFakeTimers();
    ({ game, page } = await mountPage(App, createBridge));
  });

  afterEach(() => vi.useRealTimers());

  const field = () => page.get(".rp-field__input");
  const confirmButton = () => page.findAll("button").at(-1);
  const press = async (key, init = {}) => {
    const event = new KeyboardEvent("keydown", { key, bubbles: true, cancelable: true, ...init });
    document.activeElement.dispatchEvent(event);
    await flushPromises();
  };

  async function type(text) {
    await field().setValue(text);
    await vi.advanceTimersByTimeAsync(OFFER_AFTER_MS);
  }

  it("tells the game it is there, and shows nothing until a trade opens", () => {
    expect(game.sent).toEqual([["ExchangeReady"]]);
    expect(page.find(".exchange").exists()).toBe(false);
  });

  it("opens on the other side's name with the field taking the keyboard", async () => {
    await game.emit("State", state({ theirs: { gold: 40, confirmed: true } }));

    expect(page.get("h1").text()).toBe("Trade with Lucia Gallus");
    expect(page.text()).toContain("40");
    expect(document.activeElement).toBe(field().element);
  });

  it("sends the offer once the typing stops, not at every digit", async () => {
    await game.emit("State", state());
    await field().setValue("5");
    await field().setValue("50");
    expect(game.readSent("ExchangeOffer")).toEqual([]);

    await vi.advanceTimersByTimeAsync(OFFER_AFTER_MS);
    expect(game.readSent("ExchangeOffer")).toEqual([[50]]);
  });

  it("keeps what the player types when a state arrives meanwhile", async () => {
    await game.emit("State", state());
    await field().setValue("70");
    await game.emit("State", state({ theirs: { gold: 10, confirmed: false } }));

    expect(field().element.value).toBe("70");
  });

  it("refuses what is not a whole number without sending it", async () => {
    await game.emit("State", state());
    await type("1.5");

    expect(game.readSent("ExchangeOffer")).toEqual([]);
    expect(page.get("[role=status]").text()).toBe("Give the gold in whole coins, from 0 to 120.");
  });

  it("lets confirm only once the offer being typed went", async () => {
    await game.emit("State", state());
    await field().setValue("50");
    expect(confirmButton().attributes("disabled")).toBeDefined();

    await vi.advanceTimersByTimeAsync(OFFER_AFTER_MS);
    expect(game.readSent("ExchangeOffer")).toEqual([[50]]);
    expect(confirmButton().attributes("disabled")).toBeUndefined();
  });

  it("sends the offer being typed on Enter at once, and confirms on the next", async () => {
    await game.emit("State", state());
    await field().setValue("50");
    await press("Enter");
    expect(game.readSent("ExchangeOffer")).toEqual([[50]]);
    expect(game.readSent("ExchangeConfirm")).toEqual([]);

    await press("Enter");
    expect(game.readSent("ExchangeConfirm")).toEqual([[1]]);
  });

  it("lets confirm only after the delay, with the seconds on the button", async () => {
    await game.emit("State", state({ confirmInMs: CONFIRM_DELAY_MS }));
    expect(confirmButton().attributes("disabled")).toBeDefined();
    expect(confirmButton().text()).toBe("Confirm in 3 s");

    await vi.advanceTimersByTimeAsync(CONFIRM_DELAY_MS);
    expect(confirmButton().attributes("disabled")).toBeUndefined();
    await confirmButton().trigger("click");
    expect(game.readSent("ExchangeConfirm")).toEqual([[1]]);
  });

  it("waits for the other side once confirmed", async () => {
    await game.emit("State", state({ mine: { gold: 0, confirmed: true } }));

    expect(confirmButton().text()).toBe("Waiting for Lucia Gallus");
    expect(confirmButton().attributes("disabled")).toBeDefined();
  });

  it("shows the server's refusal and the trade done", async () => {
    await game.emit("State", state());
    await game.emit("Refused", "Offer from 0 to 120 gold.");
    expect(page.get("[role=status]").text()).toBe("Offer from 0 to 120 gold.");

    await game.emit("Done", "Trade done: gave 50 gold coins");
    expect(page.get("[role=status]").text()).toBe("Trade done: gave 50 gold coins");
    expect(confirmButton().attributes("disabled")).toBeDefined();
  });

  it("calls the trade off on Escape, not on a held one", async () => {
    await game.emit("State", state());
    await press("Escape", { repeat: true });
    expect(game.readSent("ExchangeCancel")).toEqual([]);

    await press("Escape");
    expect(game.readSent("ExchangeCancel")).toEqual([[]]);
  });

  it("goes away when the game closes it", async () => {
    await game.emit("State", state());
    await game.emit("Closed");

    expect(page.find(".exchange").exists()).toBe(false);
  });
});
