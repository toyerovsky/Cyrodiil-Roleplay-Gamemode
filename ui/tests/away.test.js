import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import App from "../away/App.vue";
import { createBridge } from "../away/bridge.js";
import { formatClock } from "../away/clock.js";
import { notePresence, PRESENCE_EVERY_MS, watchPresence } from "../shared/presence.js";
import { mountPage } from "./readym.js";

describe("the AFK screen", () => {
  let game;
  let page;

  beforeEach(async () => {
    vi.useFakeTimers({ toFake: ["setInterval", "clearInterval", "Date"] });
    ({ game, page } = await mountPage(App, createBridge));
  });

  afterEach(() => vi.useRealTimers());

  const clock = () => page.find(".away__time").text();

  it("tells the game it is there and shows nothing until the server speaks", () => {
    expect(game.sent).toEqual([["AwayReady"]]);
    expect(page.find(".away").exists()).toBe(false);
  });

  it("asks whether the player is there and counts down to the mark, never below nothing", async () => {
    await game.emit("Warning", 30);
    expect(page.find(".away__title").text()).toBe("Still there?");
    expect(clock()).toBe("0:30");
    expect(page.find(".away__seen").exists()).toBe(false);

    await vi.advanceTimersByTimeAsync(3000);
    expect(clock()).toBe("0:27");

    await vi.advanceTimersByTimeAsync(60_000);
    expect(clock()).toBe("0:00");
  });

  it("says the player is away, counts the time up and says others see the mark", async () => {
    await game.emit("Marked", 291);
    expect(page.find(".away__title").text()).toBe("You are AFK");
    expect(page.find('[role="status"]').attributes("data-kind")).toBe("away");
    expect(page.find(".away__seen").text()).toBe("Others see [AFK] over your head.");
    expect(clock()).toBe("4:51");

    await vi.advanceTimersByTimeAsync(10_000);
    expect(clock()).toBe("5:01");
  });

  it("goes from the warning to the mark, and away the moment the player is back", async () => {
    await game.emit("Warning", 2);
    await game.emit("Marked", 300);
    expect(page.find(".away__title").text()).toBe("You are AFK");

    await game.emit("Ended");
    expect(page.find(".away").exists()).toBe(false);
  });
});

describe("the clock", () => {
  it("shows minutes and seconds under an hour, and hours from it", () => {
    expect(formatClock(0)).toBe("0:00");
    expect(formatClock(65)).toBe("1:05");
    expect(formatClock(3599)).toBe("59:59");
    expect(formatClock(3725)).toBe("1:02:05");
    expect(formatClock(-4)).toBe("0:00");
  });
});

describe("the player's presence in a page", () => {
  it("is reported at the first key or move, then no more often than it should", () => {
    let now = 1_000_000;
    const sent = [];
    watchPresence({ sendPresence: () => sent.push(now) }, () => now);

    window.dispatchEvent(new KeyboardEvent("keydown", { key: "a" }));
    window.dispatchEvent(new Event("pointermove"));
    expect(sent).toEqual([1_000_000]);

    now += PRESENCE_EVERY_MS;
    window.dispatchEvent(new Event("wheel"));
    expect(sent).toEqual([1_000_000, 1_000_000 + PRESENCE_EVERY_MS]);
  });

  it("takes a sign the page noted itself, the game's cursor moving, under the same pacing", () => {
    let now = 2_000_000;
    const sent = [];
    watchPresence({ sendPresence: () => sent.push(now) }, () => now);

    notePresence();
    notePresence();
    expect(sent).toEqual([2_000_000]);

    now += PRESENCE_EVERY_MS;
    notePresence();
    expect(sent).toEqual([2_000_000, 2_000_000 + PRESENCE_EVERY_MS]);
  });
});
