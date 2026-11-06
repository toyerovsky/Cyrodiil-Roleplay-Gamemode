import { flushPromises } from "@vue/test-utils";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { createBridge } from "../sanctuary/bridge.js";
import App from "../sanctuary/App.vue";
import { formatLeft } from "../sanctuary/clock.js";
import { mountPage } from "./readym.js";

const badge = (changes = {}) => JSON.stringify({ inside: true, aggressor: false, secondsLeft: 0, ...changes });

describe("the Sanctuary's badge", () => {
  let game;
  let page;

  beforeEach(async () => {
    vi.useFakeTimers();
    ({ game, page } = await mountPage(App, createBridge));
  });

  afterEach(() => {
    page.unmount();
    vi.useRealTimers();
  });

  it("asks for its state, and shows nothing outside the Sanctuary", async () => {
    expect(game.sent).toEqual([["SanctuaryReady"]]);
    await game.emit("State", badge({ inside: false }));
    expect(page.find(".sanctuary").exists()).toBe(false);
  });

  it("shows a shield while the player is inside and protected", async () => {
    await game.emit("State", badge());
    const shown = page.find(".sanctuary");
    expect(shown.attributes("aria-label")).toBe("Sanctuary");
    expect(shown.classes()).not.toContain("sanctuary--aggressor");
    expect(page.find(".sanctuary__state").text()).toBe("Protected");
  });

  it("turns to an aggressor's look with the time left, counting down", async () => {
    await game.emit("State", badge({ aggressor: true, secondsLeft: 61 }));
    expect(page.find(".sanctuary").classes()).toContain("sanctuary--aggressor");
    expect(page.find(".sanctuary__left").text()).toBe("1:01 left");

    vi.advanceTimersByTime(2000);
    await flushPromises();
    expect(page.find(".sanctuary__left").text()).toBe("0:59 left");

    vi.advanceTimersByTime(120_000);
    await flushPromises();
    expect(page.find(".sanctuary__left").text()).toBe("0:00 left");
  });

  it("goes back to protected, and away on leaving, as the server says", async () => {
    await game.emit("State", badge({ aggressor: true, secondsLeft: 30 }));
    await game.emit("State", badge());
    expect(page.find(".sanctuary__left").exists()).toBe(false);
    await game.emit("State", badge({ inside: false }));
    expect(page.find(".sanctuary").exists()).toBe(false);
  });

  it("writes itself in Polish", async () => {
    await game.emit("Locale", "pl");
    await game.emit("State", badge({ aggressor: true, secondsLeft: 5 }));
    expect(page.find(".sanctuary__title").text()).toBe("Sanktuarium");
    expect(page.find(".sanctuary__state").text()).toBe("Agresor zostało 0:05");
  });

  it("writes the time left as minutes and seconds", () => {
    expect(formatLeft(300)).toBe("5:00");
    expect(formatLeft(65.2)).toBe("1:06");
    expect(formatLeft(-3)).toBe("0:00");
  });
});
