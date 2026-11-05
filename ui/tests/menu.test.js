import { flushPromises } from "@vue/test-utils";
import { beforeEach, describe, expect, it } from "vitest";
import App from "../menu/App.vue";
import { createBridge } from "../menu/bridge.js";
import { pickTexts } from "../shared/locale.js";
import { mountPage } from "./readym.js";

const DAY_MS = 24 * 3600 * 1000;
const CREATED = Date.UTC(2026, 9, 24, 12, 0);

const state = (changes = {}) =>
  JSON.stringify({
    tab: "character",
    character: { firstName: "Marcus", lastName: "Varro", createdAt: CREATED, playedSeconds: 2 * 3600 + 5 * 60 + 30, gold: 1240 },
    account: { nickname: "Toyer", createdAt: CREATED - 30 * DAY_MS, previousVisitAt: null, sanctions: [] },
    help: [
      { category: "roleplay", commands: [{ syntax: "/me <action>", description: "what your character does" }] },
      { category: "general", commands: [{ syntax: "/players", description: "who is online, with their numbers" }] },
    ],
    dice: {
      set: "imperial",
      sets: ["imperial", "daedric", "nord"],
      recent: [
        { at: CREATED, asked: "2d6", dice: [{ sides: 6, value: 2 }, { sides: 6, value: 5 }], total: 7, reason: null },
        { at: CREATED - DAY_MS, asked: "5d20", dice: [6, 1, 17, 11, 15].map((value) => ({ sides: 20, value })), total: 50, reason: "Lazy" },
      ],
      maxDice: 5,
    },
    languages: ["en"],
    language: "en",
    ...changes,
  });

describe("the player's menu", () => {
  let game;
  let page;

  beforeEach(async () => ({ game, page } = await mountPage(App, createBridge)));

  const press = async (key, code = key) => {
    document.activeElement.dispatchEvent(new KeyboardEvent("keydown", { key, code, bubbles: true, cancelable: true }));
    await flushPromises();
  };
  // As the game sends a key: its Windows key code and no scan code, so no `code`, to whatever holds the focus.
  const pressInGame = async (keyCode, key, target = document.activeElement) => {
    target.dispatchEvent(new KeyboardEvent("keydown", { key, keyCode, bubbles: true, cancelable: true }));
    await flushPromises();
  };
  const selected = () => page.find('[role="tab"][aria-selected="true"]').attributes("data-tab");

  it("tells the game it is there, and shows nothing until the server sends the menu", () => {
    expect(game.sent).toEqual([["MenuReady"]]);
    expect(page.find(".menu").exists()).toBe(false);
  });

  it("opens on the character, under the character's illuminated name, with the tab focused", async () => {
    await game.emit("State", state());
    expect(page.find(".rp-panel__title").text()).toBe("Marcus Varro");
    expect(page.find(".rp-panel__title img").attributes("src")).toMatch(/m\.webp/);
    expect(selected()).toBe("character");
    expect(document.activeElement.dataset.tab).toBe("character");
    expect(page.find(".menu__content").text()).toContain("2 h 5 min");
    expect(page.find(".menu__content").text()).toContain("1,240 gold");
    expect(page.find(".menu__content").text()).toContain("October 24, 2026");
  });

  it("leaves out the gold the server cannot count", async () => {
    await game.emit("State", state({ character: { firstName: "Marcus", lastName: "Varro", createdAt: CREATED, playedSeconds: 59, gold: null } }));
    expect(page.find(".menu__content").text()).not.toContain("Gold carried");
    expect(page.find(".menu__content").text()).toContain("0 min");
  });

  it("goes round the tabs with Q and E and the arrows", async () => {
    await game.emit("State", state());
    await press("e", "KeyE");
    expect(selected()).toBe("account");
    await press("ArrowDown");
    expect(selected()).toBe("help");
    await press("ArrowRight");
    expect(selected()).toBe("dice");
    await press("e", "KeyE");
    expect(selected()).toBe("keys");
    await press("e", "KeyE");
    expect(selected()).toBe("character");
    await press("q", "KeyQ");
    expect(selected()).toBe("keys");
    await press("ArrowUp");
    expect(selected()).toBe("dice");
    expect(document.activeElement.dataset.tab).toBe("dice");
  });

  it("goes round the tabs with the keys as the game sends them, whatever has the focus, in either case", async () => {
    await game.emit("State", state());
    await pressInGame(69, "e");
    expect(selected()).toBe("account");
    await pressInGame(69, "E", document.body);
    expect(selected()).toBe("help");
    document.activeElement.blur();
    await pressInGame(81, "q", document.body);
    expect(selected()).toBe("account");
    await pressInGame(40, "ArrowDown");
    expect(selected()).toBe("help");
    await pressInGame(38, "ArrowUp", page.find(".menu__content").element);
    expect(selected()).toBe("account");
    await pressInGame(27, "Escape", document.body);
    expect(game.readSent("MenuClose")).toEqual([[]]);
  });

  it("takes a held key for one press", async () => {
    await game.emit("State", state());
    document.activeElement.dispatchEvent(new KeyboardEvent("keydown", { key: "e", code: "KeyE", repeat: true, bubbles: true }));
    await flushPromises();
    expect(selected()).toBe("character");
  });

  it("opens on the tab the server names, the help with its headings in order", async () => {
    await game.emit("State", state({ tab: "help" }));
    expect(selected()).toBe("help");
    expect(page.findAll(".menu__group h2").map((heading) => heading.text())).toEqual(["Roleplay", "General"]);
    expect(page.find(".menu__syntax").text()).toBe("/me <action>");
    expect(page.find(".menu__description").text()).toBe("what your character does");
  });

  it("switches tabs on a click", async () => {
    await game.emit("State", state());
    await page.find('[data-tab="keys"]').trigger("click");
    expect(selected()).toBe("keys");
    expect(page.findAll(".menu__keycap").map((key) => key.text())).toEqual(["G", "H", "Y", "N", "K", "F10", "Home", "Esc"]);
  });

  it("shows the dice set and the last rolls, picks another set and opens the board", async () => {
    await game.emit("State", state({ tab: "dice" }));
    expect(selected()).toBe("dice");
    expect(page.findAll(".menu__dice-set").map((chip) => chip.text())).toEqual(["Imperial", "Daedric", "Nord"]);
    expect(page.find('[data-set="imperial"]').attributes("aria-pressed")).toBe("true");
    expect(page.find(".menu__dice-text").text()).toBe("weathered gold, black numbers");
    const rolls = page.findAll(".menu__roll");
    expect(rolls.map((roll) => roll.find(".menu__roll-total").text())).toEqual(["7", "50"]);
    expect(rolls[0].find(".menu__roll-dice").text()).toBe("d6 [2], d6 [5]");
    expect(rolls[1].text()).toContain("for Lazy");

    await page.find('[data-set="imperial"]').trigger("click");
    await page.find('[data-set="nord"]').trigger("click");
    expect(game.readSent("MenuDiceSet")).toEqual([["nord"]]);
    await page.find(".menu__dice-open").trigger("click");
    expect(game.readSent("MenuDice")).toEqual([[]]);
  });

  it("writes the dice the Polish way in Polish", async () => {
    await game.emit("Locale", "pl");
    await game.emit("State", state({ tab: "dice", language: "pl" }));
    expect(page.findAll(".menu__roll")[0].find(".menu__roll-asked b").text()).toBe("2k6");
    expect(page.find(".menu__dice-set").text()).toBe("Cesarski");
  });

  it("names the account and says a first visit is one", async () => {
    await game.emit("State", state({ tab: "account" }));
    const text = page.find(".menu__content").text();
    expect(text).toContain("Toyer");
    expect(text).toContain("This is your first visit.");
    expect(text).toContain("None. Your record is clean.");
  });

  it("lists a sanction with its reason and its end, or that the staff must lift it", async () => {
    const ends = Date.UTC(2026, 9, 27, 15, 30);
    const account = {
      nickname: "Toyer",
      createdAt: CREATED,
      previousVisitAt: CREATED,
      sanctions: [
        { title: "Muted", reason: "Spam", endsAt: ends },
        { title: "Muted", reason: null, endsAt: null },
      ],
    };
    await game.emit("State", state({ tab: "account", account }));
    const sanctions = page.findAll(".menu__sanction");
    expect(sanctions).toHaveLength(2);
    expect(sanctions[0].text()).toContain("Reason: Spam");
    expect(sanctions[0].text()).toContain("Ends October 27, 2026");
    expect(sanctions[1].text()).toContain("Until the staff lift it");
    expect(sanctions[1].text()).not.toContain("Reason");
  });

  const languages = () => page.findAll(".menu__languages button");

  it("offers no language to pick while the server speaks one", async () => {
    await game.emit("State", state({ tab: "account" }));
    expect(page.find(".menu__languages").exists()).toBe(false);
  });

  it("offers each language in its own words and asks for another one picked", async () => {
    await game.emit("State", state({ tab: "account", languages: ["en", "pl"] }));
    expect(languages().map((language) => language.text())).toEqual(["English", "Polski"]);

    await languages()[0].trigger("click");
    await languages()[1].trigger("click");
    expect(game.readSent("MenuLanguage")).toEqual([["pl"]]);
  });

  it("names the languages as a group of toggle buttons, each a stop of Tab, the one in use pressed", async () => {
    await game.emit("State", state({ tab: "account", languages: ["en", "pl"] }));
    expect(page.find(".menu__languages").attributes()).toMatchObject({ role: "group", "aria-label": "Language" });
    expect(languages().map((language) => language.attributes("aria-pressed"))).toEqual(["true", "false"]);
    expect(languages().map((language) => language.attributes("lang"))).toEqual(["en", "pl"]);
    expect(languages().filter((language) => language.attributes("tabindex") !== undefined || language.attributes("role"))).toEqual([]);
  });

  it("asks to close on Escape, leaves F10 to the game, and goes when the game says so", async () => {
    await game.emit("State", state());
    await press("F10");
    expect(game.readSent("MenuClose")).toEqual([]);
    await press("Escape");
    expect(game.readSent("MenuClose")).toEqual([[]]);

    await game.emit("Closed");
    expect(page.find(".menu").exists()).toBe(false);
  });

  it("keeps the tab while a new state of the same tab arrives", async () => {
    await game.emit("State", state());
    await press("e", "KeyE");
    await game.emit("State", state({ tab: "account", languages: ["en", "pl"], language: "pl" }));
    expect(selected()).toBe("account");
    expect(page.find('.menu__languages [aria-pressed="true"]').text()).toBe("Polski");
  });

  it("writes itself in the language picked once the game says so", async () => {
    const polish = pickTexts("pl", "ui.menu.");
    expect(polish["tab.character"], "the menu's Polish texts").not.toBe(pickTexts("en", "ui.menu.")["tab.character"]);

    await game.emit("State", state({ tab: "account", languages: ["en", "pl"] }));
    await languages()[1].trigger("click");
    await game.emit("Locale", "pl");
    await game.emit("State", state({ tab: "account", languages: ["en", "pl"], language: "pl" }));

    expect(document.documentElement.lang).toBe("pl");
    expect(page.find('[data-tab="character"]').text()).toBe(polish["tab.character"]);
    expect(page.find(".menu__close").text()).toBe(polish.close);
    expect(languages().map((language) => language.text())).toEqual(["English", "Polski"]);
  });
});
