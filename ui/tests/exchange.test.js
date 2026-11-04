import { flushPromises } from "@vue/test-utils";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import App from "../exchange/App.vue";
import { createBridge } from "../exchange/bridge.js";
import { mountPage } from "./readym.js";

const OFFER_AFTER_MS = 400;
const CONFIRM_DELAY_MS = 3000;

const SWORD = { key: "sword", name: "Iron Longsword", used: false, count: 1 };
const WORN_SWORD = { key: "worn-sword", name: "Iron Longsword", used: true, count: 1 };
const ARROWS = { key: "arrows", name: "Iron Arrow", used: false, count: 60 };

const offer = (changes = {}) => ({ gold: 0, items: [], confirmed: false, ...changes });

const state = (changes = {}) =>
  JSON.stringify({
    version: 1,
    other: "Lucia Gallus",
    carried: 120,
    max: 120,
    confirmInMs: 0,
    items: [ARROWS, SWORD, WORN_SWORD],
    mine: offer(),
    theirs: offer(),
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

  const field = () => page.get(".exchange__side[aria-label='Your offer'] .rp-field__input");
  const filter = () => page.get(".exchange__side[aria-label='Your items'] .rp-field__input");
  const carried = () => page.findAll(".exchange__item");
  const carriedRow = (name) => carried().find((button) => button.attributes("aria-label") === `Add ${name} to your offer`);
  const pieces = () => page.get(".exchange__count");
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
    await game.emit("State", state({ theirs: offer({ gold: 40, confirmed: true }) }));

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
    await game.emit("State", state({ theirs: offer({ gold: 10 }) }));

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
    await game.emit("State", state({ mine: offer({ confirmed: true }) }));

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

  it("lists what the player can offer with what they carry", async () => {
    await game.emit("State", state());

    expect(carried().map((button) => button.text())).toEqual(["Iron Arrow×60", "Iron Longsword×1", "Iron Longsword (used)×1"]);
  });

  it("says so when the player carries nothing to trade", async () => {
    await game.emit("State", state({ items: [] }));

    expect(page.text()).toContain("You carry nothing you can trade.");
  });

  it("finds an item by a part of its name, in any case", async () => {
    await game.emit("State", state());
    await filter().setValue("ARROW");
    expect(carried().map((button) => button.text())).toEqual(["Iron Arrow×60"]);

    await filter().setValue("mace");
    expect(page.text()).toContain("No item goes by that name.");
  });

  it("offers a piece at a click once the clicks stop, and only the item that changed", async () => {
    await game.emit("State", state());
    await carriedRow("Iron Arrow").trigger("click");
    await carriedRow("Iron Arrow").trigger("click");
    expect(game.readSent("ExchangeOfferItem")).toEqual([]);

    await vi.advanceTimersByTimeAsync(OFFER_AFTER_MS);
    expect(game.readSent("ExchangeOfferItem")).toEqual([["arrows", 2]]);
    expect(game.readSent("ExchangeOffer")).toEqual([]);
  });

  it("shows what is left of a stack beside the offer, and lets none past it", async () => {
    await game.emit("State", state());
    await carriedRow("Iron Longsword").trigger("click");

    expect(carriedRow("Iron Longsword").attributes("disabled")).toBeDefined();
    expect(carriedRow("Iron Longsword").text()).toBe("Iron Longsword×0");
    expect(page.get("[aria-label='More of Iron Longsword']").attributes("disabled")).toBeDefined();
  });

  it("shows nothing left, not a debt, while a count above what the player carries waits", async () => {
    await game.emit("State", state({ mine: offer({ items: [{ ...ARROWS, count: 1 }] }) }));
    await pieces().setValue("99");

    expect(carriedRow("Iron Arrow").text()).toBe("Iron Arrow×0");
  });

  it("tells a used stack from one as made, on both sides", async () => {
    await game.emit("State", state({ theirs: offer({ items: [WORN_SWORD] }) }));
    await carriedRow("Iron Longsword (used)").trigger("click");
    await vi.advanceTimersByTimeAsync(OFFER_AFTER_MS);

    expect(game.readSent("ExchangeOfferItem")).toEqual([["worn-sword", 1]]);
    expect(page.get(".exchange__line").text()).toBe("Iron Longsword (used)×1");
  });

  it("takes a typed count and sends it once the typing stops", async () => {
    await game.emit("State", state({ mine: offer({ items: [{ ...ARROWS, count: 1 }] }) }));
    await pieces().setValue("4");
    await pieces().setValue("40");
    await vi.advanceTimersByTimeAsync(OFFER_AFTER_MS);

    expect(game.readSent("ExchangeOfferItem")).toEqual([["arrows", 40]]);
  });

  it("takes an item out of the offer at its last piece, and sends none", async () => {
    await game.emit("State", state({ mine: offer({ items: [{ ...ARROWS, count: 1 }] }) }));
    await page.get("[aria-label='Fewer of Iron Arrow']").trigger("click");
    expect(page.find(".exchange__count").exists()).toBe(false);

    await vi.advanceTimersByTimeAsync(OFFER_AFTER_MS);
    expect(game.readSent("ExchangeOfferItem")).toEqual([["arrows", 0]]);
  });

  it("refuses a count that is not whole without sending it", async () => {
    await game.emit("State", state({ mine: offer({ items: [{ ...ARROWS, count: 1 }] }) }));
    await pieces().setValue("1.5");
    await vi.advanceTimersByTimeAsync(OFFER_AFTER_MS);

    expect(game.readSent("ExchangeOfferItem")).toEqual([]);
    expect(page.get("[role=status]").text()).toBe("Give Iron Arrow in whole pieces, from 0 to 60.");
  });

  it("keeps the count being typed when a state arrives meanwhile, and takes the server's after", async () => {
    await game.emit("State", state({ mine: offer({ items: [{ ...ARROWS, count: 1 }] }) }));
    await pieces().setValue("30");
    await game.emit("State", state({ mine: offer({ items: [{ ...ARROWS, count: 1 }] }), theirs: offer({ gold: 10 }) }));
    expect(pieces().element.value).toBe("30");

    await vi.advanceTimersByTimeAsync(OFFER_AFTER_MS);
    await game.emit("State", state({ mine: offer({ items: [{ ...ARROWS, count: 20 }] }) }));
    expect(pieces().element.value).toBe("20");
  });

  it("lets confirm only once the item being offered went", async () => {
    await game.emit("State", state());
    await carriedRow("Iron Arrow").trigger("click");
    expect(confirmButton().attributes("disabled")).toBeDefined();

    await vi.advanceTimersByTimeAsync(OFFER_AFTER_MS);
    expect(confirmButton().attributes("disabled")).toBeUndefined();
  });

  it("sends the count being typed on Enter at once, and confirms on the next", async () => {
    await game.emit("State", state({ mine: offer({ items: [{ ...ARROWS, count: 1 }] }) }));
    await pieces().setValue("5");
    pieces().element.focus();
    await press("Enter");
    expect(game.readSent("ExchangeOfferItem")).toEqual([["arrows", 5]]);
    expect(game.readSent("ExchangeConfirm")).toEqual([]);

    await press("Enter");
    expect(game.readSent("ExchangeConfirm")).toEqual([[1]]);
  });

  it("freezes the items once the trade is done", async () => {
    await game.emit("State", state({ mine: offer({ items: [{ ...ARROWS, count: 1 }] }) }));
    await game.emit("Done", "Trade done: gave 1 item");

    expect(carriedRow("Iron Arrow").attributes("disabled")).toBeDefined();
    expect(pieces().attributes("disabled")).toBeDefined();
  });

  it("starts empty on the next trade", async () => {
    await game.emit("State", state({ mine: offer({ items: [{ ...ARROWS, count: 1 }] }) }));
    await game.emit("Closed");
    await game.emit("State", state());

    expect(page.find(".exchange__count").exists()).toBe(false);
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
