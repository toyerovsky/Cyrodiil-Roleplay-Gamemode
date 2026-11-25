import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import App from "../fishing/App.vue";
import { createBridge } from "../fishing/bridge.js";
import en from "../../locales/en/ui.fishing.json";
import pl from "../../locales/pl/ui.fishing.json";
import { mountPage } from "./readym.js";

const LINE = { stage: "Aim", nibbles: 0, bites: 0, bend: 0, lifting: false, tired: 0, safeFrom: 0.35, safeTo: 0.75, strikeWindowMs: 1000, outcome: null };
const text = (key) => en[`ui.fishing.${key}`];

describe("the fishing page", () => {
  let game;
  let page;

  beforeEach(async () => {
    vi.useFakeTimers({ toFake: ["setTimeout", "clearTimeout"] });
    ({ game, page } = await mountPage(App, createBridge));
  });

  afterEach(() => vi.useRealTimers());

  const show = (view) => game.emit("State", JSON.stringify({ ...LINE, ...view }));
  const title = () => page.find(".fishing__title").text();

  it("tells the game it is there and shows nothing until the line is in", () => {
    expect(game.sent).toEqual([["FishingReady"]]);
    expect(page.find(".fishing").exists()).toBe(false);
  });

  it("asks for a spot on the water with the key, and marks where the cast would land", async () => {
    await show({});
    await game.emit("Anchor", 0.25, 0.5, true);

    expect(title()).toBe(text("aim"));
    expect(page.find(".fishing__hint kbd").text()).toBe(text("key_space"));
    expect(page.find(".fishing__hint span").text()).toBe(text("aim_hint"));
    expect(page.find(".fishing__mark--aim").attributes("style")).toContain("left: 25%");
    expect(page.find(".fishing__mark--aim").attributes("style")).toContain("top: 50%");
    expect(page.find(".fishing__stop kbd").text()).toBe(text("key_move"));
    expect(page.find(".fishing__stop span").text()).toBe(text("stop_hint"));
  });

  it("asks the player to wait without the key, and says so a moment at each nibble", async () => {
    await show({ stage: "Waiting" });
    expect(title()).toBe(text("waiting"));
    expect(page.find(".fishing__hint kbd").exists()).toBe(false);

    await show({ stage: "Waiting", nibbles: 1 });
    expect(title()).toBe(text("nibble"));

    await vi.advanceTimersByTimeAsync(800);
    expect(title()).toBe(text("waiting"));
  });

  it("rings the float at the bite for as long as the strike's window lasts", async () => {
    await show({ stage: "Bite", bites: 1, strikeWindowMs: 1000 });
    await game.emit("Anchor", 0.5, 0.4, true);

    expect(title()).toBe(text("bite"));
    expect(page.find(".fishing__mark--bite .fishing__ring").attributes("style")).toContain("animation-duration: 1000ms");
  });

  it("shows the rod's bend against the safe band, and says when it is too tight or too slack", async () => {
    await show({ stage: "Landing", bend: 0.5, tired: 0.25 });
    const meter = page.find('[role="meter"]');
    expect(meter.attributes("aria-valuenow")).toBe("0.5");
    expect(page.find(".fishing__band").attributes("style")).toContain("left: 35%");
    expect(page.find(".fishing__band").attributes("style")).toContain("width: 40%");
    expect(page.find(".fishing__needle").attributes("style")).toContain("left: 50%");
    expect(page.find(".fishing__tired span").attributes("style")).toContain("width: 25%");
    expect(title()).toBe(text("landing"));

    await show({ stage: "Landing", bend: 0.9 });
    expect(title()).toBe(text("tight"));
    expect(page.find(".fishing__plate--tight").exists()).toBe(true);

    await show({ stage: "Landing", bend: 0.1 });
    expect(title()).toBe(text("slack"));
  });

  it("shows a catch on its card for a while, its size and whether it is a trophy", async () => {
    await show({ stage: "Rest", outcome: { number: 1, kind: "Caught", name: "Pike", lengthCm: 84, grams: 4446, trophy: true } });

    const card = page.find("[data-card]");
    expect(card.find(".fishing__card-name").text()).toBe("Pike");
    expect(card.find(".fishing__card-size").text()).toBe("84 cm4446 g");
    expect(card.find(".fishing__trophy").text()).toBe(text("trophy"));
    expect(title()).toBe(text("caught"));

    await vi.advanceTimersByTimeAsync(3600);
    expect(page.find("[data-card]").exists()).toBe(false);
  });

  it("shows a find without a size, and the same outcome again only once", async () => {
    const found = { number: 4, kind: "Found", name: "Flawless Pearl", lengthCm: 0, grams: 0, trophy: false };
    await show({ stage: "Rest", outcome: found });
    expect(page.find(".fishing__card-kind").text()).toBe(text("found"));
    expect(page.find(".fishing__card-size").exists()).toBe(false);

    await vi.advanceTimersByTimeAsync(3600);
    await show({ stage: "Aim", outcome: found });
    expect(page.find("[data-card]").exists()).toBe(false);
  });

  it("names each way a fish gets away, with no card", async () => {
    for (const [kind, key] of [
      ["LostEarly", "lost_early"],
      ["LostNibble", "lost_nibble"],
      ["LostLate", "lost_late"],
      ["LostSnapped", "lost_snapped"],
      ["LostSlack", "lost_slack"],
    ]) {
      await show({ stage: "Rest", outcome: { number: kind.length, kind, name: null, lengthCm: 0, grams: 0, trophy: false } });
      expect(title()).toBe(text(key));
      expect(page.find(".fishing__plate--lost").exists()).toBe(true);
      expect(page.find("[data-card]").exists()).toBe(false);
    }
  });

  it("goes when the fishing ends", async () => {
    await show({ stage: "Waiting" });
    await game.emit("Ended");

    expect(page.find(".fishing").exists()).toBe(false);
  });

  it("speaks Polish when the game says so", async () => {
    await game.emit("Locale", "pl");
    await show({ stage: "Bite", bites: 1 });

    expect(title()).toBe(pl["ui.fishing.bite"]);
  });
});

describe("the fishing page's texts", () => {
  it("are the same keys in English and in Polish", () => {
    expect(Object.keys(pl).sort()).toEqual(Object.keys(en).sort());
  });
});
