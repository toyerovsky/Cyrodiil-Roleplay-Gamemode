import { flushPromises } from "@vue/test-utils";
import { beforeEach, describe, expect, it } from "vitest";
import App from "../welcome/App.vue";
import { createBridge } from "../welcome/bridge.js";
import { pickTexts } from "../shared/locale.js";
import { mountPage } from "./readym.js";

const LAST_PLAYED = Date.UTC(2026, 9, 25, 18, 30);
const KEY_E = 69;
const KEY_N = 78;
const ENTER = 13;

const marcus = { firstName: "Marcus", lastName: "Varro", lastPlayedAt: LAST_PLAYED, playedSeconds: 2 * 3600 + 5 * 60 + 30 };
const back = (changes = {}) =>
  JSON.stringify({ nickname: "toyer", firstVisit: false, characters: [marcus], emptySlots: 0, lockedSlots: 1, ...changes });
const first = () => back({ firstVisit: true, characters: [], emptySlots: 1 });

const en = pickTexts("en", "ui.welcome.");

describe("the welcome", () => {
  let game;
  let page;

  beforeEach(async () => ({ game, page } = await mountPage(App, createBridge)));

  // As the game hands a page a key: the key code alone, no code or key, nothing focused.
  const pressInGame = async (keyCode, options = {}) => {
    document.activeElement?.blur();
    document.body.dispatchEvent(new KeyboardEvent("keydown", { keyCode, bubbles: true, cancelable: true, ...options }));
    await flushPromises();
  };

  it("tells the game it is there and shows nothing until the welcome arrives", () => {
    expect(game.sent).toEqual([["WelcomeReady"]]);
    expect(page.find(".welcome").exists()).toBe(false);
  });

  it("greets a returning player by their nickname, with their character and the locked slot", async () => {
    await game.emit("Welcome", back());

    expect(page.find("h1").text()).toBe("Welcome back, toyer");
    const slots = page.findAll(".welcome__slot");
    expect(slots).toHaveLength(2);
    expect(slots[0].find(".welcome__name").text()).toBe("Marcus Varro");
    expect(slots[0].text()).toContain("October 25, 2026");
    expect(slots[0].text()).toContain("2 h 5 min");
    expect(slots[1].text()).toContain(en.locked);
    expect(slots[1].find("button").exists()).toBe(false);
    expect(page.find(".welcome__keys").text()).toBe(en.keys_play);
  });

  it("puts the keyboard on Play as it shows", async () => {
    await game.emit("Welcome", back());
    expect(document.activeElement.textContent.trim()).toBe(en.play);
  });

  it("plays the character once, however often Play is pressed, until the game takes over", async () => {
    await game.emit("Welcome", back());
    const playButton = page.find(".welcome__slot--character button");

    await playButton.trigger("click");
    await playButton.trigger("click");

    expect(game.readSent("WelcomePlay")).toHaveLength(1);
    expect(playButton.text()).toBe(en.entering);
    expect(playButton.attributes("disabled")).toBeDefined();

    await game.emit("Closed");
    expect(page.find(".welcome").exists()).toBe(false);
  });

  it("offers a first visit a new character and no Play", async () => {
    await game.emit("Welcome", first());

    expect(page.find("h1").text()).toBe(en.title_first);
    expect(page.find(".welcome__slot--character").exists()).toBe(false);
    await page.find(".welcome__slot--empty button").trigger("click");
    expect(game.readSent("WelcomeNew")).toHaveLength(1);
    expect(game.readSent("WelcomePlay")).toHaveLength(0);
  });

  it("greets without a name a player whose nickname the game has not said yet", async () => {
    await game.emit("Welcome", back({ nickname: "" }));
    expect(page.find("h1").text()).toBe(en.title_back_unnamed);
  });

  it("plays on E and on Enter as the game hands them, the code missing and nothing focused", async () => {
    await game.emit("Welcome", back());
    await pressInGame(KEY_E);
    expect(game.readSent("WelcomePlay")).toHaveLength(1);

    await game.emit("Welcome", back());
    await pressInGame(ENTER);
    expect(game.readSent("WelcomePlay")).toHaveLength(2);
  });

  it("starts a new character on N, or on Enter when there is no character, and N does nothing with one", async () => {
    await game.emit("Welcome", back());
    await pressInGame(KEY_N, { shiftKey: true });
    expect(game.readSent("WelcomeNew")).toHaveLength(0);

    await game.emit("Welcome", first());
    await pressInGame(KEY_N, { shiftKey: true });
    expect(game.readSent("WelcomeNew")).toHaveLength(1);

    await game.emit("Welcome", first());
    await pressInGame(ENTER);
    expect(game.readSent("WelcomeNew")).toHaveLength(2);
  });

  it("keeps the keys it reads from the game and lets a held one count once", async () => {
    await game.emit("Welcome", back());
    const press = new KeyboardEvent("keydown", { key: "e", code: "KeyE", repeat: true, bubbles: true, cancelable: true });
    window.dispatchEvent(press);
    await flushPromises();

    expect(press.defaultPrevented).toBe(true);
    expect(game.readSent("WelcomePlay")).toHaveLength(0);
  });

  it("speaks the player's language", async () => {
    await game.emit("Locale", "pl");
    await game.emit("Welcome", back());

    const pl = pickTexts("pl", "ui.welcome.");
    expect(page.find("h1").text()).toBe(pl.title_back.replace("{name}", "toyer"));
    expect(page.find(".welcome__slot--character button").text()).toBe(pl.play);
    expect(page.find(".welcome__slot").text()).toContain("2 godz. 5 min");
  });
});
