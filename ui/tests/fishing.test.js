import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import App from "../fishing/App.vue";
import { createBridge } from "../fishing/bridge.js";
import { findCue, findOrder } from "../fishing/orders.js";
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
  twitch: null,
  rod: { name: "Fishing Rod (Worn)", tier: "Basic", condition: "Worn", reachCm: 700 },
  bait: { kind: "Worms", name: "Fishing Worms", count: 12 },
  skill: { level: 7, xp: 40, xpNext: 62 },
  outcome: null,
};
const FIGHT = { tension: 0.42, tensionWarn: 0.8, stamina: 0.7, distanceCm: 640, pull: "Left", lean: 0, reeling: false, slack: 0, feel: "Fair" };
const NOTHING = { species: null, name: null, lengthCm: 0, grams: 0, trophy: false, record: false, xp: 0, levelUp: null, rare: false, rod: null, baitLost: false, releaseXp: 0 };
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
  const order = () => page.find(".fishing__order");
  const hold = () => page.find('[data-cue="hold"]');
  const cues = () => page.findAll("[data-cue]").map((cue) => cue.attributes("data-cue"));
  const mark = () => page.find(".fishing__mark");
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
    expect(page.find(".fishing__hint").text()).toBe(text("aim_hint"));
    expect(page.findAll(".fishing__power-end").map((end) => end.text())).toEqual(["1.5 m", "7 m"]);
    expect(power()).toBe(0);
    expect(key("cast").text()).toBe(`${text("key_space")}${text("keys.cast")}`);
    expect(key("cast").classes()).toContain("fishing__key--wanted");
    expect(key("stop").text()).toBe(`${text("key_stop")}${text("keys.stop")}`);
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

  it("offers a twitch of the bait while waiting, lit once the water is calm", async () => {
    const twitch = { count: 0, last: "None", calmInMs: 2000, spent: false };
    await show({ stage: "Waiting", twitch });

    expect(key("twitch").text()).toBe(`${text("key_twitch")}${text("keys.twitch")}`);
    expect(key("twitch").classes()).not.toContain("fishing__key--wanted");

    await vi.advanceTimersByTimeAsync(1999);
    expect(key("twitch").classes()).not.toContain("fishing__key--wanted");
    await vi.advanceTimersByTimeAsync(1);
    expect(key("twitch").classes()).toContain("fishing__key--wanted");

    await show({ stage: "Waiting", twitch: { ...twitch, calmInMs: 0 } });
    expect(key("twitch").classes()).toContain("fishing__key--wanted");
  });

  it("says at the float when the water is calm enough for a twitch, and nothing of it when none is on offer", async () => {
    const twitch = { count: 0, last: "None", calmInMs: 2000, spent: false };
    await game.emit("Anchor", 0.5, 0.45, true);
    await show({ stage: "Waiting", twitch });
    expect(mark().classes()).toContain("fishing__mark--troubled");
    expect(mark().classes()).not.toContain("fishing__mark--calm");

    await vi.advanceTimersByTimeAsync(2000);
    expect(mark().classes()).toContain("fishing__mark--calm");
    expect(mark().classes()).not.toContain("fishing__mark--troubled");

    await show({ stage: "Waiting", twitch: { ...twitch, count: 1, last: "Drew", calmInMs: 3500 } });
    expect(mark().classes()).toContain("fishing__mark--troubled");

    for (const view of [
      { stage: "Waiting", twitch: null },
      { stage: "Waiting", twitch: { ...twitch, calmInMs: 0, spent: true } },
      { stage: "Waiting", twitch: { ...twitch, spent: true } },
      { stage: "Bite", bites: 1, twitch: { ...twitch, calmInMs: 0 } },
      { stage: "Aim", twitch: { ...twitch, calmInMs: 0 } },
    ]) {
      await show(view);
      expect(mark().classes().filter((name) => /--(calm|troubled)$/.test(name)), JSON.stringify(view)).toEqual([]);
    }
  });

  it("says for a moment what each twitch came to, and troubles the water again", async () => {
    const calm = { count: 0, last: "None", calmInMs: 0, spent: false };
    await show({ stage: "Waiting", twitch: calm });
    expect(title()).toBe(text("waiting"));

    for (const [index, last] of ["Drew", "Troubled", "Provoked", "Spooked"].entries()) {
      await show({ stage: "Waiting", twitch: { ...calm, count: index + 1, last, calmInMs: 3500 } });
      expect(title()).toBe(text(`twitch.${last}`));
      expect(key("twitch").classes()).not.toContain("fishing__key--wanted");
    }

    await vi.advanceTimersByTimeAsync(1500);
    expect(title()).toBe(text("waiting"));
  });

  it("offers no twitch once the float is pulled all the way in, and none outside the wait", async () => {
    await show({ stage: "Waiting", twitch: { count: 3, last: "Drew", calmInMs: 0, spent: true } });
    expect(key("twitch").exists()).toBe(false);
    expect(key("stop").exists()).toBe(true);

    await show({ stage: "Bite", bites: 1 });
    expect(key("twitch").exists()).toBe(false);
  });

  it("rings the float at the bite for as long as the strike's window lasts, Space wanted", async () => {
    await show({ stage: "Bite", bites: 1, strikeWindowMs: 1200 });
    await game.emit("Anchor", 0.5, 0.4, true);

    expect(title()).toBe(text("bite"));
    expect(page.find(".fishing__mark--bite .fishing__ring").attributes("style")).toContain("animation-duration: 1200ms");
    expect(key("strike").classes()).toContain("fishing__key--wanted");
  });

  it("names the fish's pull and orders the key that answers it, on the plate and at the float", async () => {
    await game.emit("Anchor", 0.5, 0.45, true);
    for (const [pull, wanted, cap, side] of [
      ["Left", "right", "key_d", "right"],
      ["Right", "left", "key_a", "left"],
      ["Rest", "reel", "key_space", "under"],
    ]) {
      await fight({ pull });
      expect(title()).toBe(text(`pull.${pull}`));
      expect(order().attributes("data-order")).toBe("do");
      expect(order().text()).toBe(`${text(cap)}${text(`order.${pull}`)}`);
      expect(hold().text()).toBe(text(cap));
      expect(hold().classes()).toContain("fishing__cue--do");
      expect(hold().attributes("data-side")).toBe(side);
      expect(cues()).toEqual(["hold"]);
      expect(page.findAll(".fishing__key--wanted").map((each) => each.attributes("data-key"))).toEqual([wanted]);
    }
  });

  it("draws the key to hold across the float from the run's arrow", async () => {
    await game.emit("Anchor", 0.5, 0.45, true);

    await fight({ pull: "Left" });
    expect(page.find(".fishing__run--left").exists()).toBe(true);
    expect(page.find(".fishing__spot--right [data-cue='hold']").text()).toBe(text("key_d"));

    await fight({ pull: "Right" });
    expect(page.find(".fishing__run--right").exists()).toBe(true);
    expect(page.find(".fishing__spot--left [data-cue='hold']").text()).toBe(text("key_a"));
  });

  it("says so in green once the order is followed, the key at the float green with it", async () => {
    await game.emit("Anchor", 0.5, 0.45, true);
    for (const [changes, said, cap] of [
      [{ pull: "Left", lean: 1 }, "order.held", "key_d"],
      [{ pull: "Right", lean: -1 }, "order.held", "key_a"],
      [{ pull: "Rest", reeling: true }, "order.Rest.done", "key_space"],
    ]) {
      await fight(changes);
      expect(order().attributes("data-order")).toBe("done");
      expect(order().text()).toBe(`${text(cap)}${text(said)}`);
      expect(order().find(".fishing__crossed").exists()).toBe(false);
      expect(hold().text()).toBe(text(cap));
      expect(hold().classes()).toContain("fishing__cue--done");
      expect(cues()).toEqual(["hold"]);
    }
  });

  it("asks for no key through a dive: Space struck out on the plate, a calm word at the float", async () => {
    await game.emit("Anchor", 0.5, 0.45, true);
    await fight({ pull: "Away" });

    expect(title()).toBe(text("pull.Away"));
    expect(order().attributes("data-order")).toBe("done");
    expect(order().classes()).toContain("fishing__order--let-go");
    expect(order().find("kbd.fishing__crossed").text()).toBe(text("key_space"));
    expect(order().find(".fishing__order-text").text()).toBe(text("order.Away"));
    expect(cues()).toEqual(["free"]);
    expect(page.find('[data-cue="free"]').text()).toBe(text("order.Away"));
    expect(page.find(".fishing__mark kbd").exists()).toBe(false);
    expect(page.findAll(".fishing__key--wanted")).toHaveLength(0);
  });

  it("orders the reel let go in red while the player reels into a run or a dive, with a sign of its own at the float", async () => {
    await game.emit("Anchor", 0.5, 0.45, true);
    for (const [changes, held] of [
      [{ pull: "Away", reeling: true }, null],
      [{ pull: "Left", lean: 0, reeling: true }, ["key_d", "do", "right"]],
      [{ pull: "Left", lean: 1, reeling: true, tension: 0.3 }, ["key_d", "done", "right"]],
      [{ pull: "Right", lean: 1, reeling: true }, ["key_a", "wrong", "left"]],
    ]) {
      await fight(changes);
      expect(order().attributes("data-order")).toBe("wrong");
      expect(order().classes()).toContain("fishing__order--let-go");
      expect(order().find("kbd.fishing__crossed").text()).toBe(text("key_space"));
      expect(order().find(".fishing__order-text").text()).toBe(text("order.force"));
      expect(key("reel").classes()).toContain("fishing__key--strain");

      const sign = page.find('.fishing__spot--under [data-cue="let-go"]');
      expect(sign.find("kbd.fishing__crossed").text()).toBe(text("key_space"));
      expect(sign.text()).toBe(`${text("key_space")}${text("let_go")}`);
      expect(page.find('[data-cue="free"]').exists()).toBe(false);

      // The key to lean with stays beside the sign: letting go is not all a run asks.
      expect(hold().exists()).toBe(!!held);
      if (!held) continue;

      const [cap, state, side] = held;
      expect(hold().text()).toBe(text(cap));
      expect(hold().classes()).toContain(`fishing__cue--${state}`);
      expect(hold().classes()).not.toContain("fishing__crossed");
      expect(hold().attributes("data-side")).toBe(side);
    }
  });

  it("orders the other key in red while the rod leans the way the fish runs", async () => {
    await game.emit("Anchor", 0.5, 0.45, true);
    for (const [changes, cap, said] of [
      [{ pull: "Left", lean: -1 }, "key_d", "order.Left.wrong"],
      [{ pull: "Right", lean: 1 }, "key_a", "order.Right.wrong"],
    ]) {
      await fight(changes);
      expect(order().attributes("data-order")).toBe("wrong");
      expect(order().classes()).not.toContain("fishing__order--let-go");
      expect(order().text()).toBe(`${text(cap)}${text(said)}`);
      expect(hold().text()).toBe(text(cap));
      expect(hold().classes()).toContain("fishing__cue--wrong");
      expect(cues()).toEqual(["hold"]);
    }
  });

  it("keeps the order's key and words each in a place of its own, whatever the order", async () => {
    for (const changes of [{ pull: "Left" }, { pull: "Left", lean: 1 }, { pull: "Right", lean: 1 }, { pull: "Away" }, { pull: "Away", reeling: true }, { pull: "Rest" }]) {
      await fight(changes);
      expect([...order().element.children].map((part) => part.className)).toEqual(["fishing__order-key", "fishing__order-text"]);
      expect(order().find(".fishing__order-key kbd").text()).not.toBe("");
    }
  });

  it("finds what the float shows for every pull, lean and reel", () => {
    const hold = (key, side, state) => ({ key, side, state });
    for (const [line, cue] of [
      [{ pull: "Rest", lean: 0, reeling: false }, { hold: hold("space", "under", "do"), letGo: false, free: false }],
      [{ pull: "Rest", lean: 1, reeling: true }, { hold: hold("space", "under", "done"), letGo: false, free: false }],
      [{ pull: "Left", lean: 0, reeling: false }, { hold: hold("d", "right", "do"), letGo: false, free: false }],
      [{ pull: "Left", lean: 1, reeling: false }, { hold: hold("d", "right", "done"), letGo: false, free: false }],
      [{ pull: "Left", lean: -1, reeling: true }, { hold: hold("d", "right", "wrong"), letGo: true, free: false }],
      [{ pull: "Right", lean: 0, reeling: true }, { hold: hold("a", "left", "do"), letGo: true, free: false }],
      [{ pull: "Right", lean: -1, reeling: false }, { hold: hold("a", "left", "done"), letGo: false, free: false }],
      [{ pull: "Right", lean: 1, reeling: false }, { hold: hold("a", "left", "wrong"), letGo: false, free: false }],
      [{ pull: "Away", lean: 1, reeling: false }, { hold: null, letGo: false, free: true }],
      [{ pull: "Away", lean: 0, reeling: true }, { hold: null, letGo: true, free: false }],
    ]) {
      expect(findCue(line), JSON.stringify(line)).toEqual(cue);
    }
  });

  it("refuses a pull it does not know, reeling or not", () => {
    for (const find of [findOrder, findCue]) {
      expect(() => find({ pull: "Up", lean: 0, reeling: false })).toThrow(/Up/);
      expect(() => find({ pull: "Up", lean: 0, reeling: true })).toThrow(/Up/);
    }
  });

  it("points the run's arrow beside the float the way the fish goes", async () => {
    await fight({ pull: "Right" });
    await game.emit("Anchor", 0.5, 0.45, true);
    expect(page.find(".fishing__mark--fight .fishing__run--right").exists()).toBe(true);

    await fight({ pull: "Rest" });
    expect(page.find(".fishing__run").exists()).toBe(false);
  });

  it("shows the lean the server took on its key, lit against the run and red with it", async () => {
    await fight({ pull: "Left", lean: 1 });
    expect(key("right").classes()).toContain("fishing__key--on");
    expect(key("right").classes()).not.toContain("fishing__key--strain");

    await fight({ pull: "Left", lean: -1 });
    expect(key("left").classes()).toEqual(expect.arrayContaining(["fishing__key--on", "fishing__key--strain"]));
  });

  it("draws the line's tension against its red zone, named for what it is", async () => {
    await fight({ tension: 0.5, tensionWarn: 0.8 });
    const tension = page.find('[data-gauge="tension"]');
    expect(tension.find(".fishing-fight__label").text()).toBe("Line tension");
    expect(tension.find('[role="meter"]').attributes("aria-label")).toBe("Line tension");
    expect(tension.find('[role="meter"]').attributes("aria-valuenow")).toBe("0.5");
    expect(tension.find(".fishing-fight__danger").attributes("style")).toContain("left: 80%");
    expect(tension.find(".fishing-fight__fill").attributes("style")).toContain("width: 50%");
    expect(tension.find(".fishing-fight__value").text()).toBe("50%");
    expect(tension.classes()).not.toContain("fishing-fight__gauge--red");

    await fight({ tension: 0.85, tensionWarn: 0.8, pull: "Away", reeling: true });
    expect(page.find('[data-gauge="tension"]').classes()).toContain("fishing-fight__gauge--red");
    expect(key("reel").classes()).toEqual(expect.arrayContaining(["fishing__key--on", "fishing__key--strain"]));
  });

  it("fills the ring round the float with the line's tension, red from the tick where the line is close to snapping", async () => {
    const dash = (part) => Number(page.find(`[data-ring="tension"] ${part}`).attributes("stroke-dasharray").split(" ")[0]);
    const turned = () => Number(page.find('[data-ring="tension"] [data-tick]').attributes("transform").match(/rotate\(([\d.]+) /)[1]);
    await game.emit("Anchor", 0.5, 0.45, true);

    await fight({ tension: 0, tensionWarn: 0.8 });
    const whole = dash(".fishing-tension__track");
    const sweep = (whole / (2 * Math.PI * Number(page.find(".fishing-tension__track").attributes("r")))) * 360;
    const tickBefore = turned();
    expect(whole).toBeGreaterThan(0);
    expect(dash("[data-fill]")).toBe(0);
    expect(page.find(".fishing__mark--fight .fishing__ring").exists()).toBe(false);
    expect(page.find('[data-ring="tension"]').classes()).not.toContain("fishing-tension--red");

    await fight({ tension: 0.5, tensionWarn: 0.8 });
    expect(dash("[data-fill]")).toBeCloseTo(whole / 2, 0);
    expect(dash(".fishing-tension__danger")).toBeCloseTo(whole * 0.2, 0);
    expect(Number(page.find(".fishing-tension__danger").attributes("stroke-dashoffset"))).toBeCloseTo(-whole * 0.8, 0);
    expect(page.find('[data-ring="tension"]').classes()).not.toContain("fishing-tension--red");

    await fight({ tension: 0.8, tensionWarn: 0.8 });
    expect(page.find('[data-ring="tension"]').classes()).toContain("fishing-tension--red");

    // The tick turns with where the red begins, by the same share of the ring as the fill.
    await fight({ tension: 1.4, tensionWarn: 0.5 });
    expect(dash("[data-fill]")).toBe(whole);
    expect(dash(".fishing-tension__danger")).toBeCloseTo(whole / 2, 0);
    expect(tickBefore - turned()).toBeCloseTo(0.3 * sweep, 0);
  });

  it("rings the float plainly outside the fight", async () => {
    await game.emit("Anchor", 0.5, 0.45, true);
    for (const view of [{}, { stage: "Waiting" }, { stage: "Bite", bites: 1 }]) {
      await show(view);
      expect(page.find(".fishing__mark .fishing__ring").exists()).toBe(true);
      expect(page.find('[data-ring="tension"]').exists()).toBe(false);
      expect(page.find("[data-cue]").exists()).toBe(false);
    }
  });

  it("shows the fish's strength, how far out it is and how heavy it feels, and leaves the rest to the order and the keys", async () => {
    await fight({ stamina: 0.25, distanceCm: 1240, feel: "Monster", reeling: true, lean: 1 });

    expect(page.find('[data-gauge="stamina"] .fishing-fight__fill').attributes("style")).toContain("width: 25%");
    expect(page.find(".fishing-fight__distance").text()).toBe("12.4 m out");
    expect(page.find(".fishing-fight__feel--monster").text()).toBe(text("feel.Monster"));
    expect(page.findAll(".fishing-fight__facts > *")).toHaveLength(2);
  });

  it("warns of a slack line only while it is slack on a resting fish, and asks to reel", async () => {
    await fight({ pull: "Rest", slack: 0 });
    expect(page.find(".fishing-fight__slack").exists()).toBe(false);

    await fight({ pull: "Rest", slack: 0.6, tension: 0.02 });
    expect(page.find('[role="alert"]').text()).toBe(text("slack"));
    expect(page.find(".fishing-fight__slack-track span").attributes("style")).toContain("width: 60%");
    expect(key("reel").classes()).toContain("fishing__key--wanted");

    await fight({ pull: "Left", slack: 0.6, tension: 0.02 });
    expect(page.find(".fishing-fight__slack").exists()).toBe(false);
    expect(key("reel").classes()).not.toContain("fishing__key--wanted");
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

  it("offers the next cast on Space and the fish's release on R, with the xp it gives, while a fish is held up", async () => {
    await rest({ kind: "Caught", species: "Pike", name: "Pike", lengthCm: 84, grams: 4446, xp: 1200, releaseXp: 1400 });

    expect(page.findAll(".fishing__key").map((shown) => shown.attributes("data-key"))).toEqual(["next", "release", "stop"]);
    expect(key("next").text()).toBe(`${text("key_space")}${text("keys.next")}`);
    expect(key("release").text()).toBe(`${text("key_release")}let it go, +1,400 xp`);
  });

  it("offers no release for a find, a fish that cannot be let go or a loss", async () => {
    await rest({ kind: "Found", name: "Pearl", xp: 3 });
    expect(page.findAll(".fishing__key").map((shown) => shown.attributes("data-key"))).toEqual(["next", "stop"]);

    await rest({ number: 2, kind: "Caught", species: "Pike", name: "Pike", lengthCm: 84, grams: 4446, xp: 31 });
    expect(page.findAll(".fishing__key").map((shown) => shown.attributes("data-key"))).toEqual(["next", "stop"]);

    await rest({ number: 3, kind: "LostSnapped", species: "Pike", name: "Pike" });
    expect(page.findAll(".fishing__key").map((shown) => shown.attributes("data-key"))).toEqual(["stop"]);
  });

  it("turns the card of a fish let go, with all it earned, and offers neither key again", async () => {
    await rest({ kind: "Caught", species: "Pike", name: "Pike", lengthCm: 84, grams: 4446, trophy: true, xp: 31, releaseXp: 10 });
    vi.advanceTimersByTime(CARD_SHOWN_MS - 1000);
    await rest({ kind: "Released", species: "Pike", name: "Pike", lengthCm: 84, grams: 4446, trophy: true, xp: 41, levelUp: 8 });

    const card = page.find("[data-card]");
    expect(title()).toBe(text("released"));
    expect(card.attributes("data-kind")).toBe("Released");
    expect(card.find(".fishing-card__eyebrow").text()).toBe(text("card.released"));
    expect(card.find(".fishing-card__name").text()).toBe("Pike");
    expect(card.find("[data-length]").text()).toBe("84 cm");
    expect(card.find("[data-xp]").text()).toBe("+41 fishing experience");
    expect(card.find("[data-level]").text()).toBe("Fishing level 8!");
    expect(card.classes()).not.toContain("fishing-card--lost");
    expect(page.findAll(".fishing__key").map((shown) => shown.attributes("data-key"))).toEqual(["stop"]);

    vi.advanceTimersByTime(CARD_SHOWN_MS - 1);
    await page.vm.$nextTick();
    expect(page.find("[data-card]").exists()).toBe(true);
    vi.advanceTimersByTime(1);
    await page.vm.$nextTick();
    expect(page.find("[data-card]").exists()).toBe(false);
  });

  it("takes the card of the last cast away when the next one lands, a loss's and a catch's alike", async () => {
    for (const [number, outcome] of [
      [1, { kind: "LostSnapped", species: "Pike", name: "Pike" }],
      [2, { kind: "Caught", species: "Pike", name: "Pike", lengthCm: 84, grams: 4446, xp: 31 }],
    ]) {
      const ended = { number, ...NOTHING, ...outcome };
      await rest({ number, ...outcome });
      expect(page.find("[data-card]").exists()).toBe(true);

      await show({ stage: "Aim", outcome: ended });
      expect(page.find("[data-card]").exists()).toBe(true);

      await show({ stage: "Waiting", outcome: ended });
      expect(page.find("[data-card]").exists()).toBe(false);

      // Its own time running out afterwards changes nothing, and the same outcome told again brings no card back.
      await show({ stage: "Waiting", nibbles: 1, outcome: ended });
      await vi.advanceTimersByTimeAsync(CARD_SHOWN_MS);
      expect(page.find("[data-card]").exists()).toBe(false);
    }
  });

  it("keeps the card while the line only waits on: a wait told again is no new cast", async () => {
    await show({ stage: "Waiting" });
    await rest({ kind: "LostEarly" });
    await show({ stage: "Aim", outcome: { number: 1, ...NOTHING, kind: "LostEarly" } });
    await show({ stage: "Aim", outcome: { number: 1, ...NOTHING, kind: "LostEarly" } });

    expect(page.find("[data-card]").exists()).toBe(true);
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

  it("shows a bare hook, naming the bait that ran out, once none of it is left", async () => {
    await show({ bait: { kind: "CrabMeat", name: "Crab Meat", count: 0 } });

    const row = page.find('[data-gear="bait"]');
    expect(row.find(".fishing-gear__name").text()).toBe(text("bait_none"));
    expect(row.find("img").exists()).toBe(false);
    expect(row.find("[data-bait-out]").text()).toBe(text("bait_out").replace("{bait}", "Crab Meat"));
    expect(row.text()).not.toContain("0 left");
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
    await game.emit("Anchor", 0.5, 0.45, true);
    await fight({ distanceCm: 1240, reeling: true });

    expect(title()).toBe(pl["ui.fishing.pull.Left"]);
    expect(page.find(".fishing-fight__distance").text()).toBe("12,4 m od ciebie");
    expect(page.find('[data-gauge="tension"] .fishing-fight__label').text()).toBe(pl["ui.fishing.tension"]);
    expect(order().text()).toBe(`${pl["ui.fishing.key_space"]}${pl["ui.fishing.order.force"]}`);
    expect(page.find('[data-cue="let-go"]').text()).toBe(`${pl["ui.fishing.key_space"]}${pl["ui.fishing.let_go"]}`);
  });
});

describe("the fishing page's texts", () => {
  it("are the same keys in English and in Polish", () => {
    expect(Object.keys(pl).sort()).toEqual(Object.keys(en).sort());
  });

  it("name every pull, feel, condition and wear the server sends", () => {
    const names = {
      pull: ["Rest", "Left", "Right", "Away"],
      order: ["Rest", "Rest.done", "Left", "Left.wrong", "Right", "Right.wrong", "held", "Away", "force"],
      feel: ["Small", "Fair", "Heavy", "Monster"],
      condition: ["Good", "Worn", "Damaged", "Broken"],
      rod_now: ["Worn", "Damaged", "Broken"],
      twitch: ["Drew", "Troubled", "Provoked", "Spooked"],
    };
    const missing = Object.entries(names).flatMap(([group, values]) => values.map((value) => `${group}.${value}`).filter((key) => !text(key)));

    expect(missing).toEqual([]);
  });

  it("name every key an order or the float asks for, and the sign to let one go", () => {
    const keys = ["key_space", "key_a", "key_d", "let_go", "tension"];

    expect(keys.filter((key) => !text(key))).toEqual([]);
    expect(keys.filter((key) => !pl[`ui.fishing.${key}`])).toEqual([]);
  });

  it("keep every order to four words at most, to be read in a glance", () => {
    const long = [en, pl].flatMap((texts) =>
      Object.entries(texts)
        .filter(([key, words]) => key.startsWith("ui.fishing.order.") && words.split(" ").length > 4)
        .map(([, words]) => words),
    );

    expect(long).toEqual([]);
  });

  it("hold no text the page stopped showing", () => {
    const gone = ["reeling", "lean.left", "lean.upright", "lean.right"];

    expect(gone.filter((key) => text(key) !== undefined || pl[`ui.fishing.${key}`] !== undefined)).toEqual([]);
  });
});
