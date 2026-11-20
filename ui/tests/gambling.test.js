import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { flushPromises } from "@vue/test-utils";
import App from "../gambling/App.vue";
import { createBridge } from "../gambling/bridge.js";
import { mountPage } from "./readym.js";
import { boardForSeat } from "../gambling/table.js";

vi.mock("../gambling/TableBoard.vue", () => ({ default: {
  props: ["seat", "label", "picked"], template: '<div class="board-stand-in" :data-seat="seat" :data-picked="picked" />',
} }));

const table = (match = null, changes = {}) => JSON.stringify({ table: 3, minStake: 1, maxStake: 500, cutPercent: 0,
  roundsToWin: 2, maxReplays: 3, inviteSeconds: 30, decisionSeconds: 45, rules: false,
  opponents: [{ number: 3, name: "Lucia Gallus", refusal: null }], opponent: 3, match, ...changes });
const match = (changes = {}) => ({ id: 81, version: 4, mode: "Poker", phase: "Reroll", busy: false, stake: 40, pot: 80,
  seat: 0, turnSeat: -1, picks: [0, 0], throws: [], activeThrow: null, throwElapsedMs: 0, animationMs: 3000, roundWinner: -1, round: 1, replay: 0, wins: [0, 0], you: "Marcus Varro", opponent: "Lucia Gallus", yourHand: [4, 4, 1, 2, 6], opponentHand: [5, 5, 2, 2, 6],
  yourRank: "Pair", opponentRank: "Two pairs", yourTurn: false, confirmed: false, raiseLimit: 40, callAmount: 10, leftMs: 45000, winner: -1, result: null, ...changes });

describe("gambling through the real page bridge", () => {
  let game, page;
  beforeEach(async () => { ({ game, page } = await mountPage(App, createBridge)); });
  afterEach(() => { vi.useRealTimers(); });
  const button = (text) => page.findAll("button").find((item) => item.text() === text);
  const key = async (value, options = {}) => { window.dispatchEvent(new KeyboardEvent("keydown", { key: value, bubbles: true, cancelable: true, ...options })); await flushPromises(); };

  it("starts empty and lists Dice Poker before Zenithar's Favor", async () => {
    expect(game.readSent("GamblingReady")).toEqual([[]]);
    expect(page.find(".gambling").exists()).toBe(false);
    await game.emit("State", table());
    expect(page.findAll(".gambling__games button").map((item) => item.text())).toEqual(["Dice Poker", "Zenithar's Favor"]);
    expect(page.find('.gambling__games [aria-pressed="true"]').text()).toBe("Dice Poker");
  });

  it("invites with whole gold only and prevents duplicate requests", async () => {
    await game.emit("State", table());
    await page.find('input[inputmode="numeric"]').setValue("40.5");
    await page.find("form").trigger("submit");
    expect(game.readSent("GamblingInvite")).toEqual([]);
    expect(page.find("input").attributes("aria-invalid")).toBe("true");
    await page.find("input").setValue("40");
    await page.find("form").trigger("submit");
    await page.find("form").trigger("submit");
    expect(game.readSent("GamblingInvite")).toEqual([[3, 40, "Poker"]]);
  });

  it("picks dice by click and CEF number keys and confirms once with Space", async () => {
    await game.emit("State", table(match()));
    await page.findAll("button.gambling__die")[0].trigger("click");
    await key("", { keyCode: 53 });
    await key(" ");
    await key(" ", { repeat: true });
    expect(game.readSent("GamblingAct")).toEqual([[81, 4, "Select", 1], [81, 4, "Select", 17], [81, 4, "Pick", 17]]);
    expect(page.findAll("button.gambling__die").every((die) => die.element.disabled)).toBe(true);
  });

  it("keeps all dice when confirming none and never sends dice values", async () => {
    await game.emit("State", table(match()));
    await key("Enter");
    expect(game.readSent("GamblingAct")).toEqual([[81, 4, "Pick", 0]]);
  });

  it("ignores composing keys and cannot act after the timer ends", async () => {
    vi.useFakeTimers();
    await game.emit("State", table(match({ leftMs: 500 })));
    await key("1", { isComposing: true });
    expect(page.find('[aria-pressed="true"]').exists()).toBe(false);
    await vi.advanceTimersByTimeAsync(1000);
    await key("Enter");
    expect(game.readSent("GamblingAct")).toEqual([]);
  });

  it("offers raises only on your turn and caps them by the server limit", async () => {
    await game.emit("State", table(match({ phase: "Raise", yourTurn: true, raiseLimit: 7 })));
    await page.find("input").setValue("8");
    expect(button("Raise").element.disabled).toBe(true);
    await page.find("input").setValue("7");
    await button("Raise").trigger("click");
    expect(game.readSent("GamblingAct")).toEqual([[81, 4, "Raise", 7]]);
  });

  it("a refusal releases the request lock and remains visible", async () => {
    await game.emit("State", table(match({ phase: "Call", yourTurn: true })));
    await button("Call 10 gold").trigger("click");
    await game.emit("Refused", "You do not carry 10 gold.");
    expect(page.find('[role="alert"]').text()).toBe("You do not carry 10 gold.");
    await button("Fold match").trigger("click");
    expect(game.readSent("GamblingAct").at(-1)).toEqual([81, 4, "Fold", 0]);
  });

  it("Escape hides without folding, including a CEF key without code", async () => {
    await game.emit("State", table(match()));
    await key("", { keyCode: 27 });
    expect(game.readSent("GamblingClose")).toEqual([[]]);
    expect(game.readSent("GamblingAct")).toEqual([]);
    await game.emit("Closed");
    expect(page.find(".gambling").exists()).toBe(false);
  });

  it("shows both rules anywhere and reads Polish game names", async () => {
    await game.emit("Locale", "pl");
    await game.emit("State", table(null, { table: null, rules: true, opponents: [] }));
    expect(page.findAll(".gambling__rules h2").map((item) => item.text())).toEqual(["Kościany poker", "Łaska Zenithara"]);
    expect(page.findAll(".gambling__ranks li")).toHaveLength(9);
  });
  it.each([0, 1])("puts the viewer's own seat %i above the opponent and keeps the board model tied to the seat", async (seat) => {
    await game.emit("State", table(match({ seat })));
    expect(page.findAll(".gambling__hand").map((hand) => Number(hand.attributes("data-seat")))).toEqual([seat, 1 - seat]);
    expect(page.findAll(".gambling__seat-info h3").map((name) => name.text())).toEqual(["Marcus Varro", "Lucia Gallus"]);
    expect(boardForSeat(0)).toBe("board");
    expect(boardForSeat(1)).toBe("board-negative");
  });

  it.each([0, 1])("announces viewer-known names for rolling from viewer seat %i", async (seat) => {
    const opponent = "A wandering Breton from the mountains above Chorrol";
    await game.emit("State", table(match({ seat, phase: "Rolling", busy: true, turnSeat: seat, opponent })));
    expect(page.find('[role="status"]').text()).toContain("Now rolling: Marcus Varro");
    await game.emit("State", table(match({ seat, phase: "Rolling", busy: true, turnSeat: 1 - seat, opponent })));
    expect(page.find('[role="status"]').text()).toContain(`Now rolling: ${opponent}`);
    await key("Enter");
    expect(game.readSent("GamblingAct")).toEqual([]);
  });

  it("keeps optimistic picks while an older acknowledgement arrives and relays the opponent's latest picks", async () => {
    await game.emit("State", table(match()));
    await key("1");
    await key("5");
    await game.emit("State", table(match({ picks: [1, 8] })));
    expect(page.findAll('.gambling__hand')[0].findAll('[aria-pressed="true"]')).toHaveLength(2);
    expect(page.findAll('.board-stand-in')[1].attributes("data-picked")).toBe("8");
    await game.emit("State", table(match({ picks: [17, 10] })));
    await key("Enter");
    expect(game.readSent("GamblingAct").at(-1)).toEqual([81, 4, "Pick", 17]);
  });

  it("uses the recognition text in call, turn and result banners", async () => {
    const opponent = "A wandering Breton from the mountains above Chorrol";
    await game.emit("State", table(match({ opponent, phase: "Call", yourTurn: true })));
    expect(page.find('[role="status"]').text()).toContain(opponent);
    expect(page.find('[role="status"]').text()).toContain("Marcus Varro");
    await game.emit("State", table(match({ opponent, phase: "Finished", winner: 1, result: "You lost the match." })));
    expect(page.find('[role="status"]').text()).toContain(opponent);
  });

});
