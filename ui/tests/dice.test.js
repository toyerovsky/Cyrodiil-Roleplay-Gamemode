import { flushPromises } from "@vue/test-utils";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import App from "../dice/App.vue";
import { createBridge } from "../dice/bridge.js";
import { pickTexts } from "../shared/locale.js";
import { mountPage } from "./readym.js";

// The three.js board does not draw in jsdom: a stand-in keeps what the page asked of it, and a test lands its throws.
const board = vi.hoisted(() => ({ options: null, calls: [] }));
vi.mock("../dice/board/scene.js", () => ({
  createBoard(options) {
    board.options = options;
    const record = (name) => (...args) => board.calls.push([name, ...args]);
    return {
      probe: {},
      start: async () => {},
      useSet: async (...args) => record("useSet")(...args),
      showTray: async (...args) => record("showTray")(...args),
      resize: record("resize"),
      setPresent: record("setPresent"),
      throwDice: record("throwDice"),
      arrive: record("arrive"),
      refuse: record("refuse"),
      pickRandomSets: (count) => Array.from({ length: count }, (_, i) => `set${i}`),
    };
  },
}));

const t = pickTexts("en", "ui.dice.");
const SETS = ["altmer", "argonian", "ayleid", "bosmer", "breton", "daedric", "dunmer", "imperial", "khajiit", "nord", "orc", "redguard"];
const AT = Date.UTC(2026, 10, 5, 18, 30);

const state = (changes = {}) =>
  JSON.stringify({
    set: "imperial",
    sets: SETS,
    recent: [
      { at: AT, asked: "1d20", dice: [{ sides: 20, value: 20 }], total: 20, reason: null },
      { at: AT - 60_000, asked: "5d20", dice: [6, 1, 17, 11, 15].map((value) => ({ sides: 20, value })), total: 50, reason: "Lazy" },
    ],
    maxDice: 5,
    ...changes,
  });

const callsOf = (name) => board.calls.filter(([called]) => called === name).map(([, ...args]) => args);
const land = async (counts, values) => {
  board.options.onLanded(counts, values);
  await flushPromises();
};

describe("the dice board", () => {
  let game;
  let page;

  beforeEach(async () => {
    localStorage.clear();
    board.calls = [];
    ({ game, page } = await mountPage(App, createBridge));
  });

  afterEach(() => {
    page.unmount();
    vi.useRealTimers();
  });

  const press = async (init) => {
    window.dispatchEvent(new KeyboardEvent("keydown", { bubbles: true, cancelable: true, ...init }));
    await flushPromises();
  };
  const button = (name) => page.findAll("button").find((each) => each.text().trim() === name || each.attributes("aria-label") === name);
  const asked = () => page.find(".dice__asked b").text();

  it("tells the game it is there and shows nothing until the server opens it", () => {
    expect(game.sent).toEqual([["DiceReady"]]);
    expect(page.find(".dice__layout").exists()).toBe(false);
    expect(page.find(".dice").classes()).not.toContain("dice--shown");
  });

  it("opens on the tray with one d20, the server's set and the last rolls, newest first", async () => {
    await game.emit("State", state({ set: "daedric" }));
    expect(page.find(".dice").classes()).toContain("dice--open");
    expect(asked()).toBe("1d20");
    expect(page.find(".dice__asked small").text()).toBe(t.placeholder);
    expect(page.find('[data-set="daedric"]').attributes("aria-pressed")).toBe("true");
    expect(callsOf("useSet").at(-1)[0]).toBe("daedric");
    const rolls = page.findAll(".dice__roll");
    expect(rolls.map((roll) => roll.find(".dice__roll-total").text())).toEqual(["20", "50"]);
    expect(rolls[0].classes()).toContain("dice__roll--high");
    expect(rolls[1].text()).toContain("for Lazy");
    expect(rolls[1].find(".dice__roll-dice").text()).toBe("d20 [6], d20 [1], d20 [17], d20 [11], d20 [15]");
    expect(document.activeElement.classList).toContain("dice__throw");
  });

  it("takes dice and a set by the mouse, up to the server's most", async () => {
    await game.emit("State", state());
    await button(`One d6 more`).trigger("click");
    await button(`One d6 more`).trigger("click");
    await button(`One d100 more`).trigger("click");
    expect(asked()).toBe("2d6 + 1d100");
    expect(callsOf("showTray").at(-1)).toEqual([{ d6: 2, d10: 0, d20: 0, d100: 1 }]);
    await button(`One d6 fewer`).trigger("click");
    expect(asked()).toBe("1d6 + 1d100");

    for (const _ of [1, 2, 3]) await button(`One d10 more`).trigger("click");
    expect(page.find(".dice__asked small").text()).toBe("5 of 5 dice");
    await button(`One d10 more`).trigger("click");
    expect(page.find(".dice__asked small").text()).toBe("Up to 5 dice a throw.");

    await button(t["set.orc"]).trigger("click");
    expect(game.readSent("DiceSet")).toEqual([["orc"]]);
    expect(page.find('[data-set="orc"]').attributes("aria-pressed")).toBe("true");
    expect(page.text()).toContain(t["set_text.orc"]);
  });

  it("throws the tray, lands the server's numbers and keeps the new list until the dice are down", async () => {
    await game.emit("State", state());
    await button(`One d6 more`).trigger("click");
    await button(`One d20 more`).trigger("click");
    await page.find(".dice__throw").trigger("click");

    expect(game.readSent("DiceThrow")).toEqual([[1, 1, 0, 1, 0]]);
    expect(callsOf("throwDice")).toEqual([[1, { d6: 1, d10: 0, d20: 1, d100: 0 }]]);
    expect(page.find(".dice__banner").text()).toBe(t.throwing);
    expect(page.find(".dice__throw").attributes("disabled")).toBeDefined();

    await game.emit("Thrown", 1, "[4,17]");
    expect(callsOf("arrive")).toEqual([[1, [4, 17]]]);
    const after = { at: AT + 1000, asked: "1d6 + 1d20", dice: [{ sides: 6, value: 4 }, { sides: 20, value: 17 }], total: 21, reason: null };
    await game.emit("State", state({ recent: [after] }));
    expect(page.findAll(".dice__roll")).toHaveLength(2);

    await land({ d6: 1, d10: 0, d20: 1, d100: 0 }, [4, 17]);
    expect(page.find(".dice__banner").text()).toBe("You rolled 1d6 + 1d20: d6 [4], d20 [17] = 21");
    expect(page.findAll(".dice__roll")).toHaveLength(1);
    expect(page.find(".dice__throw").attributes("disabled")).toBeUndefined();
  });

  it("ignores an answer to another throw and says why one was refused", async () => {
    await game.emit("State", state());
    await page.find(".dice__throw").trigger("click");
    await game.emit("Thrown", 7, "[3]");
    expect(callsOf("arrive")).toEqual([]);

    await game.emit("Refused", 1, "The dice are still rolling.");
    expect(callsOf("refuse")).toEqual([[1]]);
    expect(page.find(".dice__banner").classes()).toContain("dice__banner--warn");
    expect(page.find(".dice__banner").text()).toBe("The dice are still rolling.");
    expect(page.find(".dice__throw").attributes("disabled")).toBeUndefined();
  });

  it("is played from the keys as the game sends them, a held key once, Home left to the game", async () => {
    await game.emit("State", state());
    await press({ keyCode: 50 });
    await press({ keyCode: 50, repeat: true });
    await press({ keyCode: 52 });
    expect(asked()).toBe("1d10 + 1d100");
    await press({ key: "ArrowUp", code: "ArrowUp" });
    await press({ key: "ArrowRight", code: "ArrowRight" });
    expect(asked()).toBe("2d10 + 1d100");
    await press({ keyCode: 53 });
    expect(asked()).toBe("1d20");

    await press({ keyCode: 86 });
    expect(page.find(".dice").classes()).toContain("dice--dark");
    expect(localStorage.getItem("cyrodiilrp.dice.dark")).toBe("1");
    await press({ keyCode: 77 });
    expect(button(t.sound).attributes("aria-pressed")).toBe("false");

    await press({ key: "Home", code: "Home", keyCode: 36 });
    expect(game.readSent("DiceClose")).toEqual([]);

    await press({ keyCode: 32 });
    expect(game.readSent("DiceThrow")).toEqual([[1, 0, 0, 1, 0]]);
    await press({ key: "Escape", code: "Escape" });
    expect(game.readSent("DiceClose")).toEqual([[]]);
    await game.emit("Closed");
    expect(page.find(".dice__layout").exists()).toBe(false);
  });

  it("throws the dice /roll named on the open board, and the tray as it is for a bare /roll", async () => {
    await game.emit("State", state());
    await game.emit("ThrowOnBoard", 0, 2, 0, 0);
    expect(asked()).toBe("2d10");
    expect(game.readSent("DiceThrow")).toEqual([[1, 0, 2, 0, 0]]);
    await land({ d6: 0, d10: 2, d20: 0, d100: 0 }, [3, 9]);

    await game.emit("ThrowOnBoard", 0, 0, 0, 0);
    expect(game.readSent("DiceThrow").at(-1)).toEqual([2, 0, 2, 0, 0]);
  });

  it("does nothing for /roll while the board is shut", async () => {
    await game.emit("ThrowOnBoard", 0, 2, 0, 0);
    expect(game.readSent("DiceThrow")).toEqual([]);
  });

  it("shows a roll made for something else on the small board, in a set a die, then says it is done", async () => {
    vi.useFakeTimers();
    await game.emit("Present", "Lazy", 0, 0, 5, 0, "[6,1,17,11,15]");
    expect(page.find(".dice").classes()).toContain("dice--present");
    expect(page.find(".dice__layout").exists()).toBe(false);
    expect(callsOf("setPresent")).toEqual([[true]]);
    expect(callsOf("throwDice")).toEqual([[1, { d6: 0, d10: 0, d20: 5, d100: 0 }, ["set0", "set1", "set2", "set3", "set4"]]]);
    expect(callsOf("arrive")).toEqual([[1, [6, 1, 17, 11, 15]]]);
    expect(page.find(".dice__caption").text()).toBe("Rolling for Lazy...");

    await land({ d6: 0, d10: 0, d20: 5, d100: 0 }, [6, 1, 17, 11, 15]);
    expect(page.find(".dice__caption").text()).toBe("Lazy: 5d20 = 50");
    expect(game.readSent("DicePresented")).toEqual([]);
    await vi.advanceTimersByTimeAsync(6000);
    expect(game.readSent("DicePresented")).toEqual([[]]);
    expect(callsOf("setPresent").at(-1)).toEqual([false]);
    expect(page.find(".dice__caption").exists()).toBe(false);
  });

  it("lands a roll made for something else on the board when it is open", async () => {
    await game.emit("State", state());
    await game.emit("Present", "Brave", 0, 0, 5, 0, "[2,4,6,8,10]");
    expect(callsOf("setPresent")).toEqual([]);
    expect(callsOf("throwDice").at(-1)).toEqual([1, { d6: 0, d10: 0, d20: 5, d100: 0 }]);
    await land({ d6: 0, d10: 0, d20: 5, d100: 0 }, [2, 4, 6, 8, 10]);
    expect(page.find(".dice__banner").text()).toBe("Brave: 5d20 = 30");
    expect(game.readSent("DicePresented")).toEqual([]);
  });

  it("writes the dice the Polish way in Polish", async () => {
    await game.emit("Locale", "pl");
    await game.emit("State", state());
    expect(asked()).toBe("1k20");
    expect(page.findAll(".dice__roll")[1].find(".dice__roll-dice").text()).toBe("k20 [6], k20 [1], k20 [17], k20 [11], k20 [15]");
  });

  it("keeps the tray for the next time the page loads", async () => {
    await game.emit("State", state());
    await button(`One d6 more`).trigger("click");
    expect(JSON.parse(localStorage.getItem("cyrodiilrp.dice.tray"))).toMatchObject({ d6: 1, d20: 0, placeholder: false });
  });
});
