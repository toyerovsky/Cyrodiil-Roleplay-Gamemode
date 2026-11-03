import { beforeEach, describe, expect, it } from "vitest";
import App from "../frozen/App.vue";
import { createBridge } from "../frozen/bridge.js";
import { mountPage } from "./readym.js";

describe("the frozen notice", () => {
  let game;
  let page;

  beforeEach(async () => ({ game, page } = await mountPage(App, createBridge)));

  it("tells the game it is there and shows nothing until the staff freeze the character", () => {
    expect(game.sent).toEqual([["FrozenReady"]]);
    expect(page.find(".frozen").exists()).toBe(false);
  });

  it("says why while frozen and goes when thawed", async () => {
    await game.emit("Notice", "The staff froze you: Running from the guards");
    expect(page.find('[role="alert"]').text()).toContain("Running from the guards");
    expect(page.find(".frozen__title").text()).toBe("Frozen by the staff");

    await game.emit("Thawed");
    expect(page.find(".frozen").exists()).toBe(false);
  });

  it("keeps the keys from the game while frozen", async () => {
    await game.emit("Notice", "The staff froze you: Spam");
    const press = new KeyboardEvent("keydown", { key: "w", bubbles: true, cancelable: true });
    window.dispatchEvent(press);
    expect(press.defaultPrevented).toBe(true);
  });
});
