import { beforeEach, describe, expect, it, vi } from "vitest";
import texts from "../../locales/en/ui.mentor.json";
import App from "../mentor/App.vue";
import { createBridge } from "../mentor/bridge.js";
import { pictureOf } from "../mentor/pictures.js";
import { mountPage } from "./readym.js";

const HOUR_MS = 60 * 60 * 1000;
const NAME_KEY = "ui.mentor.name.";
const CAPS = { trainedSkill: 60, limitedSkill: 30, trainedAttribute: 75, limitedAttribute: 50, unchosenSkill: 50 };
const MAGES = [
  { id: "Wizard", path: "Mage", mastered: ["Intelligence", "Destruction"], trained: ["Alchemy", "HandToHand"], majors: ["Destruction", "Alchemy"] },
  { id: "Alchemist", path: "Mage", mastered: ["Alchemy", "Restoration"], trained: ["Destruction"], majors: ["Alchemy"] },
  { id: "Illusionist", path: "Mage", mastered: ["Illusion"], trained: ["Sneak"], majors: ["Illusion", "Sneak"] },
];

const state = ({ path = "Mage", chosen = null, gold = 1340, price = 0, nextChoiceAt = null, offered = MAGES } = {}) =>
  JSON.stringify({
    path,
    offered: offered.map((each) => ({ ...each, chosen: each.id === chosen })),
    chosen,
    gold,
    price,
    nextChoiceAt,
    caps: CAPS,
  });

describe("the mentor's window", () => {
  let game;
  let page;

  beforeEach(async () => ({ game, page } = await mountPage(App, createBridge)));

  const row = (id) => page.find(`[data-specialization="${id}"]`);
  const button = (id) => row(id).find("button");
  const readSum = (name) => page.find(`[data-sum="${name}"] dd`).text();

  it("tells the game it is there, and shows nothing until a mentor opens it", () => {
    expect(game.sent).toEqual([["MentorReady"]]);
    expect(page.find(".mentor").exists()).toBe(false);
  });

  it("lists what this mentor teaches with the stats each masters and trains, by the game's names", async () => {
    await game.emit("State", state());
    expect(page.find(".rp-panel__title, h1, h2").text()).toBe("The mage's path");
    expect(page.findAll("[data-specialization]").map((each) => each.find(".mentor__name").text())).toEqual(["Wizard", "Alchemist", "Illusionist"]);
    expect(row("Wizard").find('[data-stats="mastered"]').text()).toBe("Masters, to 100: Intelligence, Destruction");
    expect(row("Wizard").find('[data-stats="trained"]').text()).toBe("Trained, to 60: Alchemy, Hand to Hand");
    expect(row("Wizard").find('[data-stats="majors"]').text()).toBe("Major skills, which level you up: Destruction, Alchemy");
    expect(readSum("specialization")).toBe("None yet");
    expect(readSum("gold")).toBe("1,340 gold");
    expect(readSum("next")).toBe("Free");
    expect(page.find(".mentor__note").text()).toBe("Until you choose, every skill stops at 50.");
    expect(page.find(".mentor__rest").text()).toBe(
      "Every other skill stops at 30 and every other attribute at 50. Your potions fortify only what you master, and Speed.",
    );
    expect(document.activeElement).toBe(button("Wizard").element);
  });

  it("chooses a first specialization at once and waits for the mentor", async () => {
    await game.emit("State", state());
    await button("Alchemist").trigger("click");
    expect(game.readSent("MentorChoose")).toEqual([["Alchemist"]]);
    expect(page.find(".mentor__status").text()).toBe("The mentor writes your name in the rolls...");
    expect(button("Wizard").element.disabled).toBe(true);

    await game.emit("Answer", "You are an Alchemist now.", true);
    await game.emit("State", state({ chosen: "Alchemist", price: 1000, nextChoiceAt: Date.now() + 72 * HOUR_MS }));
    expect(page.find(".mentor__status--done").text()).toBe("You are an Alchemist now.");
    expect(readSum("specialization")).toBe("Alchemist");
    expect(button("Alchemist").text()).toBe("Yours");
  });

  it("asks before a change and sends nothing when the player keeps theirs", async () => {
    await game.emit("State", state({ chosen: "Wizard", gold: 2500, price: 1000 }));
    expect(readSum("next")).toBe("1,000 gold");
    expect(button("Wizard").element.disabled).toBe(true);

    await button("Illusionist").trigger("click");
    expect(page.find(".mentor__question").text()).toBe(
      "Change your specialization to Illusionist for 1,000 gold? Your skills above its limits drop to them for good.",
    );
    expect(game.readSent("MentorChoose")).toEqual([]);

    await page.find('[data-action="cancel"]').trigger("click");
    expect(page.find(".mentor__confirm").exists()).toBe(false);
    expect(game.readSent("MentorChoose")).toEqual([]);

    await button("Illusionist").trigger("click");
    await page.find('[data-action="confirm"]').trigger("click");
    expect(game.readSent("MentorChoose")).toEqual([["Illusionist"]]);
  });

  it("holds every choice while a change must wait, and says how long in its largest unit, rounded up", async () => {
    await game.emit("State", state({ chosen: "Alchemist", gold: 5000, price: 1000, nextChoiceAt: Date.now() + 70.2 * HOUR_MS }));
    expect(readSum("next")).toBe("In 71 h");
    for (const id of ["Wizard", "Alchemist", "Illusionist"]) expect(button(id).element.disabled).toBe(true);
    expect(button("Wizard").text()).toBe("Not yet");

    await game.emit("State", state({ chosen: "Alchemist", gold: 5000, price: 1000, nextChoiceAt: Date.now() + 90 * 1000 }));
    expect(readSum("next")).toBe("In 2 min");
  });

  it("counts a short wait down by the second and opens the choices when it is over", async () => {
    vi.useFakeTimers({ toFake: ["setInterval", "clearInterval", "Date"] });
    try {
      await game.emit("State", state({ chosen: "Alchemist", gold: 5000, price: 1000, nextChoiceAt: Date.now() + 2500 }));
      expect(readSum("next")).toBe("In 3 s");
      expect(button("Wizard").element.disabled).toBe(true);

      vi.advanceTimersByTime(1000);
      await page.vm.$nextTick();
      expect(readSum("next")).toBe("In 2 s");

      vi.advanceTimersByTime(2000);
      await page.vm.$nextTick();
      expect(readSum("next")).toBe("1,000 gold");
      expect(button("Wizard").element.disabled).toBe(false);
      expect(button("Wizard").text()).toBe("Choose");
    } finally {
      vi.useRealTimers();
    }
  });

  it("refuses a change the gold carried does not pay for", async () => {
    await game.emit("State", state({ chosen: "Wizard", gold: 999, price: 1000 }));
    expect(button("Alchemist").element.disabled).toBe(true);
    expect(button("Alchemist").attributes("aria-label")).toBe("Not enough gold: Alchemist");
  });

  it("names the character's specialization of another path at the registrar", async () => {
    await game.emit("State", state({ path: null, chosen: "Knight", offered: MAGES }));
    expect(page.find(".rp-panel__title, h1, h2").text()).toBe("Imperial Registry");
    expect(readSum("specialization")).toBe("Knight");
  });

  it("draws each specialization's own picture, as decoration beside its name", async () => {
    await game.emit("State", state());
    for (const { id } of MAGES) {
      const picture = row(id).find("img.mentor__picture");
      expect(picture.attributes("src")).toBe(pictureOf(id));
      expect(picture.attributes("alt")).toBe("");
    }
  });

  it("has a picture for every specialization it can name, each its own", () => {
    const ids = Object.keys(texts)
      .filter((key) => key.startsWith(NAME_KEY))
      .map((key) => key.slice(NAME_KEY.length));
    expect(ids).toHaveLength(9);
    const pictures = ids.map(pictureOf);
    expect(pictures.every(Boolean)).toBe(true);
    expect(new Set(pictures).size).toBe(ids.length);
  });

  it("shows a refusal and lets the player choose again", async () => {
    await game.emit("State", state({ chosen: "Wizard", gold: 2500, price: 1000 }));
    await button("Alchemist").trigger("click");
    await page.find('[data-action="confirm"]').trigger("click");
    await game.emit("Answer", "You changed your specialization not long ago.", false);
    expect(page.find(".mentor__status--refused").text()).toBe("You changed your specialization not long ago.");
    expect(button("Alchemist").element.disabled).toBe(false);
  });

  it("asks the game to close on Esc, after Esc closed the question first", async () => {
    await game.emit("State", state({ chosen: "Wizard", gold: 2500, price: 1000 }));
    await button("Alchemist").trigger("click");
    window.dispatchEvent(new KeyboardEvent("keydown", { key: "Escape" }));
    await page.vm.$nextTick();
    expect(page.find(".mentor__confirm").exists()).toBe(false);
    expect(game.readSent("MentorClose")).toEqual([]);

    window.dispatchEvent(new KeyboardEvent("keydown", { key: "Escape" }));
    expect(game.readSent("MentorClose")).toEqual([[]]);
    await game.emit("Closed");
    expect(page.find(".mentor").exists()).toBe(false);
  });
});
