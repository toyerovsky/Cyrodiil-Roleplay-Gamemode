import { flushPromises } from "@vue/test-utils";
import { beforeEach, describe, expect, it } from "vitest";
import App from "../chat/App.vue";
import { createBridge } from "../chat/bridge.js";
import { CHAT_HISTORY_LINES } from "../shared/limits.js";
import { mountPage } from "./readym.js";

const line = (kind, text) => ({ kind, text, at: Date.UTC(2026, 9, 23, 12, 30) });
const lines = (...batch) => JSON.stringify(batch);

describe("the chat", () => {
  let game;
  let page;

  beforeEach(async () => {
    ({ game, page } = await mountPage(App, createBridge));
    await game.emit("History", lines(line("Say", "Lucia Gallus says: Bread!"), line("Action", "* Ra'jirr counts his coins.")));
  });

  const entry = () => page.get(".chat__entry");
  const press = async (key, init = {}) => {
    const event = new KeyboardEvent("keydown", { key, bubbles: true, cancelable: true, ...init });
    entry().element.dispatchEvent(event);
    await flushPromises();
    return event;
  };

  async function say(text) {
    await entry().setValue(text);
    await press("Enter");
  }

  it("asks for what was said before it loaded", () => {
    expect(game.sent[0]).toEqual(["RequestHistory"]);
  });

  it("shows each line in the colour of its kind", () => {
    const shown = page.findAll(".chat-line");
    expect(shown.map((l) => l.classes().find((c) => c.startsWith("chat-line--")))).toEqual(["chat-line--Say", "chat-line--Action"]);
    expect(shown[0].text()).toContain("Lucia Gallus says: Bread!");
  });

  it("adds new lines after the history and keeps no more than the server does", async () => {
    await game.emit("Line", lines(line("Staff", "[Staff] toyer: Market event at dusk.")));
    expect(page.findAll(".chat-line").at(-1).classes()).toContain("chat-line--Staff");

    const many = Array.from({ length: CHAT_HISTORY_LINES + 5 }, (_, i) => line("Say", `line ${i}`));
    await game.emit("Line", JSON.stringify(many));
    expect(page.findAll(".chat-line")).toHaveLength(CHAT_HISTORY_LINES);
  });

  it("sends a line on Enter and stays open for the next one", async () => {
    await game.emit("Open", true);
    await say("  good morning  ");

    expect(game.readSent("Submit")).toEqual([["good morning"]]);
    expect(entry().element.value).toBe("");
    expect(page.get(".chat").classes()).toContain("chat--open");
  });

  it("sends nothing for a held Enter or one an input method is still composing", async () => {
    await game.emit("Open", true);
    await entry().setValue("hello");
    await press("Enter", { repeat: true });
    await press("Enter", { isComposing: true });

    expect(game.readSent("Submit")).toEqual([]);
  });

  it("brings back what was sent with the arrows, and the line being typed after them", async () => {
    await game.emit("Open", true);
    await say("first");
    await say("second");
    await entry().setValue("half a");

    await press("ArrowUp");
    expect(entry().element.value).toBe("second");
    await press("ArrowUp");
    await press("ArrowUp");
    expect(entry().element.value).toBe("first");
    await press("ArrowDown");
    await press("ArrowDown");
    expect(entry().element.value).toBe("half a");
  });

  it("leaves out the K that opened it, and its repeats, but takes the next K", async () => {
    await game.emit("Open", true);
    const isTyped = (data) => {
      const event = new InputEvent("beforeinput", { data, inputType: "insertText", bubbles: true, cancelable: true });
      entry().element.dispatchEvent(event);
      return !event.defaultPrevented;
    };

    expect(isTyped("k")).toBe(false);
    expect((await press("k", { repeat: true })).defaultPrevented).toBe(true);
    entry().element.dispatchEvent(new KeyboardEvent("keyup", { key: "k", bubbles: true }));
    expect((await press("k")).defaultPrevented).toBe(false);
    expect(isTyped("k")).toBe(true);
  });

  it("asks the game to close on Escape", async () => {
    await game.emit("Open", true);
    await press("Escape");
    expect(game.readSent("Close")).toEqual([[]]);
  });

  it("keeps the keyboard on Tab", async () => {
    await game.emit("Open", true);
    expect((await press("Tab")).defaultPrevented).toBe(true);
  });

  it("tells the game when something else took the keyboard, and does not hand it back itself", async () => {
    await game.emit("Open", true);
    entry().element.blur();
    await flushPromises();

    expect(game.readSent("Blurred")).toEqual([[]]);
    expect(game.readSent("Close")).toEqual([]);
  });

  it("keeps the log up after closing, until it fades", async () => {
    await game.emit("Open", true);
    await game.emit("Open", false);
    expect(page.get(".chat").classes()).not.toContain("chat--idle");
  });

  it("takes the start of a command from the wheel, for the player to finish", async () => {
    await game.emit("Open", true);
    await game.emit("Draft", "/pm 3 ");

    expect(entry().element.value).toBe("/pm 3 ");
    expect(entry().element.selectionStart).toBe("/pm 3 ".length);
    await entry().setValue("/pm 3 hello");
    await press("Enter");
    expect(game.readSent("Submit")).toEqual([["/pm 3 hello"]]);
  });

  it("follows the locale the game names", async () => {
    await game.emit("Locale", "pl");
    expect(document.documentElement.lang).toBe("pl");
  });
});
