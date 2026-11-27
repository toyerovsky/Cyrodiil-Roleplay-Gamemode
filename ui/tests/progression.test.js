import { describe, expect, it } from "vitest";
import App from "../progression/App.vue";
import { createBridge } from "../progression/bridge.js";
import { mountPage } from "./readym.js";

const state = (more = {}) => JSON.stringify({ enabled: true, level: 1, xp: 250, next: 800, perkPoints: 0, ...more });

describe("the experience bar", () => {
  it("asks for its state and stays hidden before playing or while disabled", async () => {
    const { page, game } = await mountPage(App, createBridge);
    expect(game.sent).toEqual([["ProgressionReady"]]);
    expect(page.find("section").exists()).toBe(false);
    await game.emit("State", state({ enabled: false }));
    expect(page.find("section").exists()).toBe(false);
  });

  it("shows XP and updates the level and unspent points after gaining a level", async () => {
    const { page, game } = await mountPage(App, createBridge);
    await game.emit("State", state());
    expect(page.text()).toContain("Level 1");
    expect(page.text()).toContain("250 / 800 XP");
    expect(page.get('[role="progressbar"]').attributes("aria-valuenow")).toBe("250");
    expect(page.get(".progression__fill").attributes("style")).toContain("31.25%");
    await game.emit("State", state({ level: 2, xp: 25, next: 900, perkPoints: 1 }));
    expect(page.text()).toContain("Level 2");
    expect(page.text()).toContain("Perk points: 1");
    expect(page.get('[role="progressbar"]').attributes("aria-valuemax")).toBe("900");
  });

  it("shows the cap without dividing by zero and translates live into Polish", async () => {
    const { page, game } = await mountPage(App, createBridge);
    await game.emit("State", state({ level: 40, xp: 0, next: 0, perkPoints: 39 }));
    expect(page.text()).toContain("Maximum level");
    expect(page.get(".progression__fill").attributes("style")).toContain("100%");
    await game.emit("Locale", "pl");
    expect(page.text()).toContain("Poziom 40");
    expect(page.text()).toContain("Punkty talentów: 39");
    await game.emit("State", state({ enabled: false }));
    expect(page.find("section").exists()).toBe(false);
  });
});
