import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import App from "../fishing/App.vue";
import { createBridge } from "../fishing/bridge.js";
import en from "../../locales/en/ui.fishing.json";
import pl from "../../locales/pl/ui.fishing.json";
import { mountPage } from "./readym.js";

const CHARGE_UP_MS = 1100;
const CARD_SHOWN_MS = 6000;
const LINE = {
  stage: "Aim",
  nibbles: 0,
  bites: 0,
  strikeWindowMs: 1000,
  fight: null,
  rod: { name: "Fishing Rod (Worn)", tier: "Basic", condition: "Worn", reachCm: 700 },
  bait: { kind: "Worms", name: "Fishing Worms", count: 12 },
  skill: { level: 7, xp: 40, xpNext: 62 },
  outcome: null,
};
const FIGHT = { tension: 0.42, tensionWarn: 0.8, stamina: 0.7, distanceCm: 640, pull: "Left", lean: 0, reeling: false, slack: 0, feel: "Fair" };
const NOTHING = { species: null, name: null, lengthCm: 0, grams: 0, trophy: false, record: false, xp: 0, levelUp: null, rare: false, rod: null, baitLost: false };
const text = (key) => en[`ui.fishing.${key}`];

describe("the fishing page", () => {
  let game;
  let page;

  beforeEach(async () => {
    vi.useFakeTimers({ toFake: ["setTimeout", "clearTimeout", "requestAnimationFrame", "cancelAnimationFrame", "performance"] });
    ({ game, page } = await mountPage(App, createBridge));
  });

  afterEach(() => vi.useRealTimers());

  const show = (view) => game.emit("State", JSON.stringify({ ...LINE, ...view }));
  const fight = (changes) => show({ stage: "Fight", bites: 1, fight: { ...FIGHT, ...changes } });
  const rest = (outcome, view = {}) => show({ stage: "Rest", bites: 1, outcome: { number: 1, ...NOTHING, ...outcome }, ...view });
  const title = () => page.find(".fishing__title").text();
  const key = (id) => page.find(`[data-key="${id}"]`);
  const power = () => Number(page.find(".fishing__power-track").attributes("aria-valuenow"));

  it("tells the game it is there and shows nothing until the line is in", () => {
    expect(game.sent).toEqual([["FishingReady"]]);
    expect(page.find(".fishing").exists()).toBe(false);
  });

  it("asks for a cast, from the nearest to the rod's reach, and marks where it would land", async () => {
    await show({});
    await game.emit("Anchor", 0.25, 0.5, true);

    expect(title()).toBe(text("aim"));
    expect(page.findAll(".fishing__power-end").map((end) => end.text())).toEqual(["1.5 m", "7 m"]);
    expect(power()).toBe(0);
    expect(key("cast").text()).toBe(`${text("key_space")}${text("keys.cast")}`);
    expect(key("cast").classes()).toContain("fishing__key--wanted");
    expect(key("stop").text()).toBe(`${text("key_e")}${text("keys.stop")}`);
    expect(page.find(".fishing__mark--aim").attributes("style")).toContain("left: 25%");
    expect(page.find(".fishing__mark--aim").attributes("style")).toContain("top: 50%");
  });

  it("swings the cast's power up and down on its own clock while Space is held, and holds it when let go", async () => {
    await show({});
    await game.emit("Anchor", 0.5, 0.5, true);
    await game.emit("Charge", true, CHARGE_UP_MS);
    expect(key("cast").classes()).toContain("fishing__key--on");
    expect(page.find(".fishing__mark--charging").exists()).toBe(true);

    await vi.advanceTimersByTimeAsync(CHARGE_UP_MS / 2);
    expect(power()).toBeCloseTo(0.5, 1);
    await vi.advanceTimersByTimeAsync(CHARGE_UP_MS / 2);
    expect(power()).toBeCloseTo(1, 1);
    await vi.advanceTimersByTimeAsync(CHARGE_UP_MS * 0.75);
    expect(power()).toBeCloseTo(0.25, 1);

    await game.emit("Charge", false, 0);
    const held = power();
    await vi.advanceTimersByTimeAsync(CHARGE_UP_MS);
    expect(power()).toBe(held);
    expect(page.find(".fishing__power-track--charging").exists()).toBe(false);
  });

  it("starts the power from nothing at the next cast", async () => {
    await show({});
    await game.emit("Charge", true, CHARGE_UP_MS);
    await vi.advanceTimersByTimeAsync(CHARGE_UP_MS / 2);
    await game.emit("Charge", false, 0);
    await show({ stage: "Waiting" });
    await show({ stage: "Aim" });

    expect(power()).toBe(0);
  });

  it("asks the player to wait, and says so a moment at each nibble with a ripple at the float", async () => {
    await show({ stage: "Waiting" });
    await game.emit("Anchor", 0.5, 0.45, true);
    expect(title()).toBe(text("waiting"));
    expect(page.find(".fishing__hint").text()).toBe(text("waiting_hint"));
    expect(page.find(".fishing__ripple").exists()).toBe(false);

    await show({ stage: "Waiting", nibbles: 1 });
    expect(title()).toBe(text("nibble"));
    expect(page.find(".fishing__ripple").exists()).toBe(true);

    await vi.advanceTimersByTimeAsync(800);
    expect(title()).toBe(text("waiting"));
  });

  it("rings the float at the bite for as long as the strike's window lasts, Space wanted", async () => {
    await show({ stage: "Bite", bites: 1, strikeWindowMs: 1200 });
    await game.emit("Anchor", 0.5, 0.4, true);

    expect(title()).toBe(text("bite"));
    expect(page.find(".fishing__mark--bite .fishing__ring").attributes("style")).toContain("animation-duration: 1200ms");
    expect(key("strike").classes()).toContain("fishing__key--wanted");
  });

  it("names the fish's pull and the key that counters it", async () => {
    for (const [pull, wanted] of [
      ["Left", "right"],
      ["Right", "left"],
      ["Rest", "reel"],
    ]) {
      await fight({ pull });
      expect(title()).toBe(text(`pull.${pull}`));
      expect(page.find(".fishing__hint").text()).toBe(text(`counter.${pull}`));
      expect(page.findAll(".fishing__key--wanted").map((each) => each.attributes("data-key"))).toEqual([wanted]);
    }

    await fight({ pull: "Away" });
    expect(title()).toBe(text("pull.Away"));
    expect(page.findAll(".fishing__key--wanted")).toHaveLength(0);
  });

  it("points the run's arrow beside the float the way the fish goes", async () => {
    await fight({ pull: "Right" });
    await game.emit("Anchor", 0.5, 0.45, true);
    expect(page.find(".fishing__mark--fight .fishing__run--right").exists()).toBe(true);

    await fight({ pull: "Rest" });
    expect(page.find(".fishing__run").exists()).toBe(false);
  });

  it("shows the lean the server took, green against the run and red with it", async () => {
    await fight({ pull: "Left", lean: 1 });
    expect(key("right").classes()).toContain("fishing__key--on");
    expect(page.find(".fishing-fight__lean--against").text()).toBe(text("lean.right"));
    expect(page.find(".fishing__hint--done").exists()).toBe(true);

    await fight({ pull: "Left", lean: -1 });
    expect(key("left").classes()).toEqual(expect.arrayContaining(["fishing__key--on", "fishing__key--strain"]));
    expect(page.find(".fishing-fight__lean--with").text()).toBe(text("lean.left"));
    expect(page.find(".fishing__hint--done").exists()).toBe(false);
  });

  it("draws the line's tension against its red zone, and marks reeling into a run in the red", async () => {
    await fight({ tension: 0.5, tensionWarn: 0.8 });
    const tension = page.find('[data-gauge="tension"]');
    expect(tension.find('[role="meter"]').attributes("aria-valuenow")).toBe("0.5");
    expect(tension.find(".fishing-fight__danger").attributes("style")).toContain("left: 80%");
    expect(tension.find(".fishing-fight__fill").attributes("style")).toContain("width: 50%");
    expect(tension.find(".fishing-fight__value").text()).toBe("50%");
    expect(tension.classes()).not.toContain("fishing-fight__gauge--red");

    await fight({ tension: 0.85, tensionWarn: 0.8, pull: "Away", reeling: true });
    expect(page.find('[data-gauge="tension"]').classes()).toContain("fishing-fight__gauge--red");
    expect(key("reel").classes()).toEqual(expect.arrayContaining(["fishing__key--on", "fishing__key--strain"]));
    expect(page.find(".fishing-fight__reeling").text()).toBe(text("reeling"));
  });

  it("shows the fish's strength, how far out it is and how heavy it feels", async () => {
    await fight({ stamina: 0.25, distanceCm: 1240, feel: "Monster" });

    expect(page.find('[data-gauge="stamina"] .fishing-fight__fill').attributes("style")).toContain("width: 25%");
    expect(page.find(".fishing-fight__distance").text()).toBe("12.4 m out");
    expect(page.find(".fishing-fight__feel--monster").text()).toBe(text("feel.Monster"));
  });

  it("warns of a slack line only while it is slack, and asks to reel", async () => {
    await fight({ slack: 0 });
    expect(page.find(".fishing-fight__slack").exists()).toBe(false);

    await fight({ slack: 0.6, tension: 0.02 });
    expect(page.find('[role="alert"]').text()).toBe(text("slack"));
    expect(page.find(".fishing-fight__slack-track span").attributes("style")).toContain("width: 60%");
    expect(key("reel").classes()).toContain("fishing__key--wanted");
  });

  it("shows a catch on its card: the painted fish, its size, the trophy, the record, the experience and the level", async () => {
    await rest(
      { kind: "Caught", species: "GoldenCarp", name: "Emperor's Golden Carp", lengthCm: 88, grams: 11176, trophy: true, record: true, rare: true, xp: 270, levelUp: 46 },
    );

    const card = page.find("[data-card]");
    expect(title()).toBe(text("caught"));
    expect(card.find(".fishing-card__eyebrow").text()).toBe(text("card.caught"));
    expect(card.find(".fishing-card__name").text()).toBe("Emperor's Golden Carp");
    expect(card.find(".fishing-card__picture img").attributes("src")).toContain("goldencarp");
    expect(card.find("[data-length]").text()).toBe("88 cm");
    expect(card.find("[data-weight]").text()).toBe("11.2 kg");
    expect(card.findAll("[data-badge]").map((badge) => badge.text())).toEqual([text("trophy"), text("record"), text("rare")]);
    expect(card.find("[data-xp]").text()).toBe("+270 fishing experience");
    expect(card.find("[data-level]").text()).toBe("Fishing level 46!");
    expect(card.classes()).toContain("fishing-card--rare");

    await vi.advanceTimersByTimeAsync(CARD_SHOWN_MS);
    expect(page.find("[data-card]").exists()).toBe(false);
  });

  it("weighs a small fish in grams", async () => {
    await rest({ kind: "Caught", species: "Betty", name: "River Betty", lengthCm: 12, grams: 19, xp: 6 });

    expect(page.find("[data-weight]").text()).toBe("19 g");
    expect(page.find("[data-badge]").exists()).toBe(false);
    expect(page.find("[data-level]").exists()).toBe(false);
  });

  it("shows a find without a size, and the same outcome again only once", async () => {
    const found = { kind: "Found", name: "Flawless Pearl", xp: 3 };
    await rest(found);
    expect(page.find(".fishing-card__eyebrow").text()).toBe(text("card.found"));
    expect(page.find(".fishing-card__picture--find").exists()).toBe(true);
    expect(page.find(".fishing-card__size").exists()).toBe(false);

    await vi.advanceTimersByTimeAsync(CARD_SHOWN_MS);
    await show({ stage: "Aim", outcome: { number: 1, ...NOTHING, ...found } });
    expect(page.find("[data-card]").exists()).toBe(false);
  });

  it("names each way a fish gets away, on the plate and on a card", async () => {
    const kinds = ["LostEarly", "LostNibble", "LostLate", "LostSnapped", "LostSlack", "LostRunaway"];
    for (const [index, kind] of kinds.entries()) {
      const key = kind.replace(/^Lost/, "lost_").toLowerCase();
      await rest({ number: index + 1, kind });
      expect(title()).toBe(text(key));
      expect(page.find(".fishing__plate--lost").exists()).toBe(true);
      expect(page.find(".fishing-card__eyebrow").text()).toBe(text("card.lost"));
      expect(page.find(".fishing-card__name").text()).toBe(text(key));
    }
  });

  it("says what a loss cost: the rod worn down and the lure gone", async () => {
    await rest({ kind: "LostRunaway", species: "Longfin", name: "Abecean Longfin", rod: "Broken", baitLost: true });

    const card = page.find("[data-card]");
    expect(card.find(".fishing-card__lost-name").text()).toBe("Abecean Longfin");
    expect(card.find("[data-rod]").text()).toBe(text("rod_now.Broken"));
    expect(card.find("[data-bait]").text()).toBe(text("lure_lost"));
  });

  it("keeps the rod, the bait and the skill in the corner", async () => {
    await show({});

    const gear = page.find(".fishing-gear");
    expect(gear.find('[data-gear="rod"] .fishing-gear__name').text()).toBe("Fishing Rod (Worn)");
    expect(gear.find(".fishing-gear__condition--worn").text()).toBe(text("condition.Worn"));
    expect(gear.find('[data-gear="rod"]').text()).toContain("casts 7 m");
    expect(gear.find('[data-gear="rod"] img').attributes("src")).toContain("rod");
    expect(gear.find('[data-gear="bait"] .fishing-gear__name').text()).toBe("Fishing Worms");
    expect(gear.find('[data-gear="bait"] img').attributes("src")).toContain("worms");
    expect(gear.find('[data-gear="bait"]').text()).toContain("12 left");
    expect(gear.find('[data-gear="skill"]').text()).toContain("level 7");
    expect(gear.find(".fishing-gear__xp span").attributes("style")).toContain("width: 64.5%");
    expect(gear.find(".fishing-gear__xp-text").text()).toBe("40 / 62 xp");
  });

  it("says when no bait is on the hook", async () => {
    await show({ bait: null });

    expect(page.find('[data-gear="bait"] .fishing-gear__name').text()).toBe(text("bait_none"));
    expect(page.find('[data-gear="bait"] img').exists()).toBe(false);
  });

  it("goes when the fishing ends, the power with it", async () => {
    await show({});
    await game.emit("Charge", true, CHARGE_UP_MS);
    await game.emit("Ended");
    await vi.advanceTimersByTimeAsync(CHARGE_UP_MS);

    expect(page.find(".fishing").exists()).toBe(false);
  });

  it("takes no focus and holds nothing a key could reach", async () => {
    await fight({});

    expect(page.findAll("button, input, [tabindex]")).toHaveLength(0);
  });

  it("speaks Polish when the game says so", async () => {
    await game.emit("Locale", "pl");
    await fight({ distanceCm: 1240 });

    expect(title()).toBe(pl["ui.fishing.pull.Left"]);
    expect(page.find(".fishing-fight__distance").text()).toBe("12,4 m od ciebie");
  });
});

describe("the fishing page's texts", () => {
  it("are the same keys in English and in Polish", () => {
    expect(Object.keys(pl).sort()).toEqual(Object.keys(en).sort());
  });

  it("name every pull, feel, condition and wear the server sends", () => {
    const names = {
      pull: ["Rest", "Left", "Right", "Away"],
      counter: ["Rest", "Left", "Right", "Away"],
      feel: ["Small", "Fair", "Heavy", "Monster"],
      condition: ["Good", "Worn", "Damaged"],
      rod_now: ["Worn", "Damaged", "Broken"],
    };
    const missing = Object.entries(names).flatMap(([group, values]) => values.map((value) => `${group}.${value}`).filter((key) => !text(key)));

    expect(missing).toEqual([]);
  });
});
