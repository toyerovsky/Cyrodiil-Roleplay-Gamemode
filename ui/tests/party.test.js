import { describe, expect, it } from "vitest";
import App from "../party/App.vue";
import { createBridge } from "../party/bridge.js";
import { mountPage } from "./readym.js";

const MAX = 6;
const party = (members) => JSON.stringify({ members, max: MAX });
const member = (name, more = {}) => ({ name, leader: false, self: false, health: 100, away: false, ...more });

describe("the party panel", () => {
  it("asks for the party, and shows nothing while the player is in none", async () => {
    const { game, page } = await mountPage(App, createBridge);
    expect(game.sent).toEqual([["PartyReady"]]);
    await game.emit("State", party([]));
    expect(page.find(".party").exists()).toBe(false);
  });

  it("lists the members as the server orders them, the player by name marked as you, the leader crowned", async () => {
    const { game, page } = await mountPage(App, createBridge);
    await game.emit("State", party([member("Lucia Gallus", { leader: true }), member("Marius Varro", { self: true })]));

    const rows = page.findAll(".party__member");
    expect(rows.map((row) => row.find(".party__name").text())).toEqual(["Lucia Gallus", "Marius Varro"]);
    expect(rows[0].find(".party__crown").exists()).toBe(true);
    expect(rows[1].classes()).toContain("party__member--self");
    expect(rows[1].find(".party__mark").text()).toBe("you");
    expect(page.find(".party__count").text()).toBe(`2/${MAX}`);
  });

  it("shows each member's health, one down, one far away and one not in the world without a bar", async () => {
    const { game, page } = await mountPage(App, createBridge);
    await game.emit("State", party([
      member("Marius Varro", { leader: true, self: true }),
      member("Lucia Gallus", { health: 45 }),
      member("Ra'jirr", { health: 0 }),
      member("Mazoga gro-Malog", { away: true }),
      member("Hjalti Early-Beard", { health: null, away: true }),
    ]));

    const rows = page.findAll(".party__member");
    expect(rows[1].find(".party__health").attributes("aria-valuenow")).toBe("45");
    expect(rows[1].find(".party__mark").exists()).toBe(false);
    expect(rows[2].classes()).toContain("party__member--down");
    expect(rows[2].find(".party__mark").text()).toBe("down");
    expect(rows[3].classes()).toContain("party__member--away");
    expect(rows[3].find(".party__mark").text()).toBe("far away");
    expect(rows[4].find(".party__health").exists()).toBe(false);
    expect(rows[4].find(".party__mark").text()).toBe("far away");
  });

  it("goes when the party ends", async () => {
    const { game, page } = await mountPage(App, createBridge);
    await game.emit("State", party([member("Lucia Gallus", { leader: true }), member("Marius Varro", { self: true })]));
    await game.emit("State", party([]));
    expect(page.find(".party").exists()).toBe(false);
  });
});
