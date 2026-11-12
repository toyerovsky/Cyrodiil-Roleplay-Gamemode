import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import App from "../notices/App.vue";
import { createBridge } from "../notices/bridge.js";
import { mountPage } from "./readym.js";

const NOTICES = [
  { id: 7, text: "Lost: a silver ring.\nA reward for its return.", ageMinutes: 30, mine: false },
  { id: 5, text: "Wine bought by the barrel.", ageMinutes: 3000, mine: true },
];

const state = (changes = {}) =>
  JSON.stringify({ board: 2, notices: NOTICES, shown: 10, minLength: 1, maxLength: 300, perCharacter: 3, mine: 1, waitSeconds: 0, expiryDays: 7, ...changes });

describe("the notice board's window", () => {
  let game;
  let page;

  beforeEach(async () => ({ game, page } = await mountPage(App, createBridge)));
  afterEach(() => vi.useRealTimers());

  const field = () => page.find('[data-field="notice"]');
  const click = (selector) => page.find(selector).trigger("click");
  const postButton = () => page.find('[data-action="post"]');
  const rule = () => page.find("[data-rule]").text();

  it("tells the game it is there, and shows nothing until a board opens it", () => {
    expect(game.sent).toEqual([["BoardReady"]]);
    expect(page.find(".notices").exists()).toBe(false);
  });

  it("shows the notices unsigned, newest first, and marks the reader's own", async () => {
    await game.emit("State", state());
    const shown = page.findAll(".notice");
    expect(shown.map((notice) => notice.find(".notice__text").text())).toEqual(["Lost: a silver ring.\nA reward for its return.", "Wine bought by the barrel."]);
    expect(shown[0].find(".notice__age").text()).toBe("Pinned 30 minutes ago");
    expect(shown[1].find(".notice__age").text()).toBe("Pinned 2 days ago");
    expect(shown[0].find('[data-action="take-down"]').exists()).toBe(false);
    expect(shown[1].find(".notice__yours").text()).toBe("Yours");
    expect(page.find(".notices__lead").text()).toContain("stays up for 7 days");
    expect(document.activeElement).toBe(field().element);
  });

  it("says so when nothing is pinned yet", async () => {
    await game.emit("State", state({ notices: [], mine: 0 }));
    expect(page.find(".notices__empty").text()).toBe("Nothing is pinned here yet.");
  });

  it("pins what was typed and clears it once the board took it", async () => {
    await game.emit("State", state());
    await field().setValue("Room to let, ask at the inn.");
    await page.find("form").trigger("submit");
    expect(game.readSent("BoardPost")).toEqual([["Room to let, ask at the inn."]]);
    expect(page.find(".notices__status").text()).toBe("Pinning it to the board...");
    expect(postButton().element.disabled).toBe(true);

    await game.emit("Answer", "Your notice is up on the board.", true);
    await game.emit("State", state({ mine: 2, waitSeconds: 300 }));
    expect(page.find(".notices__status--done").text()).toBe("Your notice is up on the board.");
    expect(field().element.value).toBe("");
  });

  it("pins with Ctrl+Enter from the field, and a plain Enter is only a new line", async () => {
    await game.emit("State", state());
    await field().setValue("Two lines");
    await field().trigger("keydown", { key: "Enter" });
    expect(game.readSent("BoardPost")).toEqual([]);
    await field().trigger("keydown", { key: "Enter", ctrlKey: true });
    expect(game.readSent("BoardPost")).toEqual([["Two lines"]]);
  });

  it("refuses a notice of only spaces at its field and sends nothing", async () => {
    await game.emit("State", state());
    await field().setValue("   ");
    await page.find("form").trigger("submit");
    expect(game.readSent("BoardPost")).toEqual([]);
    expect(field().attributes("aria-invalid")).toBe("true");
    expect(page.find('[role="alert"]').text()).toBe("Write a little more: a notice holds 1 to 300 letters.");
  });

  it("keeps the field to the server's length and counts it", async () => {
    await game.emit("State", state({ maxLength: 120 }));
    expect(field().attributes("maxlength")).toBe("120");
    await field().setValue("abc");
    expect(page.find("[data-count]").text()).toBe("3 / 120");
  });

  it("holds the reader back at as many notices as one may, with the reason", async () => {
    await game.emit("State", state({ mine: 3 }));
    expect(postButton().element.disabled).toBe(true);
    expect(rule()).toBe("You have as many notices up as one may. Take one of yours down to pin another.");
  });

  it("counts down to the next notice and lets the reader pin once it is over", async () => {
    vi.useFakeTimers();
    await game.emit("State", state({ waitSeconds: 125 }));
    expect(postButton().element.disabled).toBe(true);
    expect(rule()).toBe("You can pin another notice in 3 minutes.");

    await vi.advanceTimersByTimeAsync(66_000);
    expect(rule()).toBe("You can pin another notice in 1 minute.");
    await vi.advanceTimersByTimeAsync(60_000);
    expect(rule()).toBe("Your notices up: 1 of 3.");
    expect(postButton().element.disabled).toBe(false);
  });

  it("says a full board takes its oldest notice down", async () => {
    await game.emit("State", state({ shown: 2 }));
    expect(rule()).toBe("Your notices up: 1 of 3. The board is full: a new notice takes the oldest one down.");
  });

  it("takes the reader's own notice down by its id", async () => {
    await game.emit("State", state());
    await click('[data-notice="5"] [data-action="take-down"]');
    expect(game.readSent("BoardTakeDown")).toEqual([[5]]);
    expect(page.find(".notices__status").text()).toBe("Taking it down...");
    await game.emit("Answer", "You took your notice down.", true);
    expect(page.find(".notices__status--done").text()).toBe("You took your notice down.");
  });

  it("keeps the text when the server refuses it, and says why", async () => {
    await game.emit("State", state());
    await field().setValue("Again");
    await page.find("form").trigger("submit");
    await game.emit("Answer", "You can pin another notice in 4 min.", false);
    expect(page.find(".notices__status--refused").text()).toBe("You can pin another notice in 4 min.");
    expect(field().element.value).toBe("Again");
  });

  it("leaves on Escape and closes when the server says so", async () => {
    await game.emit("State", state());
    window.dispatchEvent(new KeyboardEvent("keydown", { key: "Escape", cancelable: true }));
    expect(game.readSent("BoardClose")).toEqual([[]]);
    await game.emit("Closed");
    expect(page.find(".notices").exists()).toBe(false);
  });

  it("reads in Polish to a Polish player", async () => {
    await game.emit("Locale", "pl");
    await game.emit("State", state());
    expect(postButton().text()).toBe("Przypnij");
    expect(page.find(".notices__lead").text()).toContain("przez 7 dni");
    expect(page.findAll(".notice__age")[1].text()).toBe("Przypięte przedwczoraj");
  });
});
