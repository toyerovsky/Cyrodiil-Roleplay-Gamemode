import { beforeEach, describe, expect, it } from "vitest";
import App from "../bank/App.vue";
import { createBridge } from "../bank/bridge.js";
import { BANK_MOST_AT_ONCE } from "../shared/limits.js";
import { mountPage } from "./readym.js";

const state = (changes = {}) => JSON.stringify({ balance: 1250, gold: 340, ...changes });

describe("the bank's window", () => {
  let game;
  let page;

  beforeEach(async () => ({ game, page } = await mountPage(App, createBridge)));

  const field = () => page.find('[data-field="amount"]');
  const click = (selector) => page.find(selector).trigger("click");
  const readSum = (name) => page.find(`[data-sum="${name}"] dd`).text();

  it("tells the game it is there, and shows nothing until a banker opens it", () => {
    expect(game.sent).toEqual([["BankReady"]]);
    expect(page.find(".bank").exists()).toBe(false);
  });

  it("shows the gold at the bank and the gold carried, with the amount ready to type", async () => {
    await game.emit("State", state());
    expect(readSum("balance")).toBe("1,250 gold");
    expect(readSum("gold")).toBe("340 gold");
    expect(document.activeElement).toBe(field().element);
    expect(page.find(".bank__status").text()).toContain("Type an amount");
  });

  it("deposits the amount typed and clears the field once the bank wrote it down", async () => {
    await game.emit("State", state());
    await field().setValue(" 120 ");
    await click('[data-action="deposit"]');
    expect(game.readSent("BankDeposit")).toEqual([[120]]);
    expect(page.find(".bank__status").text()).toBe("The banker writes it down...");
    expect(page.find('[data-action="deposit"]').element.disabled).toBe(true);

    await game.emit("Answer", "You put 120 gold into the bank.", true);
    await game.emit("State", state({ balance: 1370, gold: 220 }));
    expect(page.find(".bank__status").text()).toBe("You put 120 gold into the bank.");
    expect(readSum("balance")).toBe("1,370 gold");
    expect(field().element.value).toBe("");
    expect(page.find('[data-action="deposit"]').element.disabled).toBe(false);
  });

  it("withdraws the amount typed", async () => {
    await game.emit("State", state());
    await field().setValue("1000");
    await click('[data-action="withdraw"]');
    expect(game.readSent("BankWithdraw")).toEqual([[1000]]);
  });

  it.each(["", "0", "12.5", "-3", "a lot", String(BANK_MOST_AT_ONCE + 1)])("refuses the amount \"%s\" at its field and sends nothing", async (typed) => {
    await game.emit("State", state({ gold: BANK_MOST_AT_ONCE * 2 }));
    await field().setValue(typed);
    await click('[data-action="deposit"]');
    expect(game.readSent("BankDeposit")).toEqual([]);
    expect(field().attributes("aria-invalid")).toBe("true");
    expect(page.find('[role="alert"]').text()).toBe("Give a whole amount from 1 to 1,000,000 gold.");
  });

  it("refuses more than is carried or more than the bank holds before it is sent", async () => {
    await game.emit("State", state());
    await field().setValue("341");
    await click('[data-action="deposit"]');
    expect(page.find('[role="alert"]').text()).toBe("You carry only 340 gold.");
    await field().setValue("1251");
    await click('[data-action="withdraw"]');
    expect(page.find('[role="alert"]').text()).toBe("The bank holds only 1,250 gold of yours.");
    expect(game.sent).toEqual([["BankReady"]]);

    await field().setValue("340");
    await click('[data-action="deposit"]');
    expect(page.find('[role="alert"]').exists()).toBe(false);
    expect(game.readSent("BankDeposit")).toEqual([[340]]);
  });

  it("fills in all that is carried or all that is at the bank, up to the most one move takes", async () => {
    await game.emit("State", state());
    await click('[data-fill="gold"]');
    expect(field().element.value).toBe("340");
    await click('[data-fill="balance"]');
    expect(field().element.value).toBe("1250");

    await game.emit("State", state({ balance: BANK_MOST_AT_ONCE * 3, gold: 0 }));
    await click('[data-fill="balance"]');
    expect(field().element.value).toBe(String(BANK_MOST_AT_ONCE));
    expect(page.find('[data-fill="gold"]').element.disabled).toBe(true);
  });

  it("keeps the amount when the server refuses it, and says why", async () => {
    await game.emit("State", state());
    await field().setValue("200");
    await click('[data-action="withdraw"]');
    await game.emit("Answer", "You want to take out 200 gold and the bank holds 150 of yours.", false);
    expect(page.find(".bank__status--refused").text()).toContain("the bank holds 150");
    expect(field().element.value).toBe("200");
  });

  it("moves gold once at a time, the next only after the bank answered", async () => {
    await game.emit("State", state());
    await field().setValue("10");
    await click('[data-action="deposit"]');
    await click('[data-action="deposit"]');
    await click('[data-action="withdraw"]');
    expect(game.sent.slice(1)).toEqual([["BankDeposit", 10]]);
  });

  it("leaves on Escape and closes when the server says so", async () => {
    await game.emit("State", state());
    window.dispatchEvent(new KeyboardEvent("keydown", { key: "Escape", cancelable: true }));
    expect(game.readSent("BankClose")).toEqual([[]]);
    await game.emit("Closed");
    expect(page.find(".bank").exists()).toBe(false);
  });

  it("opens again clean after it was closed with an answer on it", async () => {
    await game.emit("State", state());
    await field().setValue("999");
    await click('[data-action="deposit"]');
    await game.emit("Closed");
    await game.emit("State", state());
    expect(field().element.value).toBe("");
    expect(page.find('[role="alert"]').exists()).toBe(false);
    expect(page.find('[data-action="deposit"]').element.disabled).toBe(false);
  });

  it("reads in Polish to a Polish player", async () => {
    await game.emit("Locale", "pl");
    await game.emit("State", state());
    expect(page.find('[data-action="deposit"]').text()).toBe("Wpłać");
    expect(readSum("gold")).toBe("340 szt. złota");
  });
});
