import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import App from "../guides/App.vue";
import { createBridge } from "../guides/bridge.js";
import { PICTURES } from "../guides/pictures.js";
import { buildGuide } from "../guides/pretend.js";
import { mountPage } from "./readym.js";

const ARMED_MS = 600;

function press(code, key, options = {}) {
  const event = new KeyboardEvent("keydown", { code, key, bubbles: true, cancelable: true, ...options });
  window.dispatchEvent(event);
  return event;
}

describe("a guide's card", () => {
  let game;
  let page;

  beforeEach(async () => {
    vi.useFakeTimers({ toFake: ["setTimeout", "clearTimeout", "Date"] });
    ({ game, page } = await mountPage(App, createBridge));
  });

  afterEach(() => vi.useRealTimers());

  const open = (topic, mode) => game.emit("State", JSON.stringify(buildGuide(topic, mode)));

  it("tells the game it is there and shows nothing until a guide comes", () => {
    expect(game.sent).toEqual([["GuideReady"]]);
    expect(page.find(".guide").exists()).toBe(false);
  });

  it("shows the guide's title, purpose, numbered steps, keys, numbers and picture", async () => {
    await open("Gambling", "Use");

    expect(page.find("h1").text()).toBe("The gaming table");
    expect(page.find('[role="dialog"]').attributes("aria-labelledby")).toBe("guide-title");
    expect(page.findAll(".guide__number").map((number) => number.text())).toEqual(["I", "II", "III", "IV", "V"]);
    expect(page.findAll(".guide__step").at(0).findAll("kbd").map((key) => key.text())).toEqual(["G", "H"]);
    expect(page.findAll(".guide__fact dd").map((fact) => fact.text())).toEqual(["1 to 500 gold", "2", "30 s", "0%"]);
    expect(page.find(".guide__picture").attributes("src")).toBe(PICTURES.Gambling);
    expect(page.find(".guide__eyebrow").text()).toBe("The first time");
  });

  it("goes on with E once armed, and only once", async () => {
    await open("Bank", "Use");

    press("KeyE", "e");
    expect(game.readSent("GuideContinue")).toEqual([]);

    vi.advanceTimersByTime(ARMED_MS);
    expect(press("KeyE", "e").defaultPrevented).toBe(true);
    press("Enter", "Enter");
    expect(game.readSent("GuideContinue")).toEqual([[]]);
    expect(game.readSent("GuideClose")).toEqual([]);
  });

  it("only closes on Esc, armed or not, and a held key does nothing", async () => {
    await open("Bank", "Use");
    vi.advanceTimersByTime(ARMED_MS);

    press("KeyE", "e", { repeat: true });
    press("Escape", "Escape");

    expect(game.readSent("GuideContinue")).toEqual([]);
    expect(game.readSent("GuideClose")).toEqual([[]]);
  });

  it("offers Continue and Close for a window met the first time, the primary one focused", async () => {
    await open("Stable", "Use");

    expect(page.find("[data-go-on]").exists()).toBe(true);
    expect(document.activeElement).toBe(page.find("[data-go-on]").element);
    await page.find("[data-go-on]").trigger("click");
    expect(game.readSent("GuideContinue")).toEqual([[]]);
  });

  it("shows the welcome as keys with a line each, closed by Enter or E as much as by Esc", async () => {
    await open("Welcome", "Entry");

    expect(page.find(".guide__number").exists()).toBe(false);
    expect(page.find(".guide__steps--plain").exists()).toBe(true);
    expect(page.findAll(".guide__step").at(3).findAll("kbd").map((key) => key.text())).toEqual(["Y", "N"]);
    expect(page.find(".guide__eyebrow").text()).toBe("A good start");
    expect(page.find("[data-go-on]").exists()).toBe(false);

    vi.advanceTimersByTime(ARMED_MS);
    press("Enter", "Enter");
    expect(game.readSent("GuideClose")).toEqual([[]]);
    expect(game.readSent("GuideContinue")).toEqual([]);
  });

  it("leads back to the menu for a guide read again", async () => {
    await open("Lore", "Read");

    expect(page.find("[data-close]").text()).toContain("Back");
    expect(page.find(".guide__eyebrow").text()).toBe("Guide");
    expect(page.find(".guide__again").text()).toBe("");
  });

  it("goes when the game closes it, and comes again fresh for the next guide", async () => {
    await open("Bank", "Use");
    await game.emit("Closed");
    expect(page.find(".guide").exists()).toBe(false);

    await open("Dice", "Use");
    vi.advanceTimersByTime(ARMED_MS);
    press("Escape", "Escape");
    expect(game.readSent("GuideClose")).toEqual([[]]);
  });
});
