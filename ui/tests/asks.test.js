import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import App from "../asks/App.vue";
import { createBridge } from "../asks/bridge.js";
import { mountPage } from "./readym.js";

const TOTAL_MS = 30000;
const state = (asks, answerable) => JSON.stringify({ asks: asks.map((ask) => ({ totalMs: TOTAL_MS, ...ask })), answerable });
const lucia = { id: 1, kind: "party", name: "Lucia Gallus", incoming: true, leftMs: 27000 };
const rajirr = { id: 2, kind: "trade", name: "Ra'jirr", incoming: true, leftMs: 29000 };
const mazoga = { id: 3, kind: "party", name: "Mazoga gro-Malog", incoming: false, leftMs: 18000 };

describe("the invitations", () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  it("asks for its state, and shows nothing while nobody asks", async () => {
    const { game, page } = await mountPage(App, createBridge);
    expect(game.sent).toEqual([["AsksReady"]]);
    await game.emit("State", state([], 0));
    expect(page.find(".asks").exists()).toBe(false);
  });

  it("shows the keys on the one Y and N answer only, and the player's own ask without them", async () => {
    const { game, page } = await mountPage(App, createBridge);
    await game.emit("State", state([lucia, rajirr, mazoga], lucia.id));

    const cards = page.findAll(".ask");
    expect(cards.map((card) => card.find(".ask__text").text())).toEqual([
      "Lucia Gallus invites you to their party.",
      "Ra'jirr wants to trade with you.",
      "Waiting for Mazoga gro-Malog to answer your invitation.",
    ]);
    expect(cards[0].findAll(".ask__keycap").map((key) => key.text())).toEqual(["Y", "N"]);
    expect(cards[0].find(".ask__key").text()).toContain("Join");
    expect(cards[1].find(".ask__keys").exists()).toBe(false);
    expect(cards[1].find(".ask__next").exists()).toBe(true);
    expect(cards[2].find(".ask__keys").exists()).toBe(false);
    expect(cards[2].find(".ask__next").exists()).toBe(false);
  });

  it("counts each ask down from when it came, not from a later state", async () => {
    const { game, page } = await mountPage(App, createBridge);
    await game.emit("State", state([lucia], lucia.id));
    expect(page.find(".ask__seconds").text()).toBe("27 s");

    await vi.advanceTimersByTimeAsync(5000);
    await game.emit("State", state([lucia, rajirr], lucia.id));
    expect(page.findAll(".ask__seconds").map((each) => each.text())).toEqual(["22 s", "29 s"]);
  });

  it("goes once the last ask is answered", async () => {
    const { game, page } = await mountPage(App, createBridge);
    await game.emit("State", state([lucia], lucia.id));
    await game.emit("State", state([], 0));
    expect(page.find(".asks").exists()).toBe(false);
  });
});
