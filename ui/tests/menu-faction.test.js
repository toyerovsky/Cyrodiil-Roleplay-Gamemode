import { flushPromises } from "@vue/test-utils";
import { beforeEach, describe, expect, it, vi } from "vitest";
import App from "../menu/App.vue";
import { createBridge } from "../menu/bridge.js";
import { FACTION_NAME_MIN, FACTION_RANK_NAME_MAX } from "../shared/limits.js";
import { mountPage } from "./readym.js";

const DAY_MS = 24 * 3600 * 1000;
const FOUNDED = Date.UTC(2026, 10, 1, 18, 0);
const CONFIRM_MS = 2500;

const rank = (number, name, kind, permissions, can = {}) => ({
  number,
  name,
  kind,
  permissions,
  members: 1,
  canRename: false,
  canEdit: false,
  canDelete: false,
  canMoveUp: false,
  canMoveDown: false,
  canGive: false,
  ...can,
});

const member = (id, name, rankNumber, can = {}) => ({
  id,
  name,
  rank: rankNumber,
  online: false,
  lastSeenAt: FOUNDED + DAY_MS,
  self: false,
  canKick: false,
  canRank: false,
  ...can,
});

// The Fighters Guild as Modryn, its Champion, reads it: he acts on the Warders and the Associates below him.
const asChampion = () => ({
  id: 7,
  name: "Fighters Guild",
  foundedAt: FOUNDED,
  rank: 3,
  canInvite: true,
  canAddRank: true,
  canLeave: true,
  grantable: ["Invite", "Ranks"],
  ranks: [
    rank(1, "Master", "Leader", []),
    rank(3, "Champion", "Custom", ["Invite", "Ranks"]),
    rank(4, "Warder", "Custom", ["Invite"], { canRename: true, canEdit: true, canDelete: true, canMoveDown: false, canGive: true }),
    rank(5, "Scribe", "Custom", [], { canRename: true, canEdit: true, canDelete: true, canMoveUp: true, canGive: true }),
    rank(2, "Associate", "Entry", [], { canRename: true, canEdit: true, canGive: true }),
  ],
  members: [
    member(1, "Vilena Donton", 1, { online: true }),
    member(2, "Modryn Oreyn", 3, { self: true, online: true }),
    member(3, "Burz gro-Khash", 4, { canKick: true, canRank: true }),
    member(5, "Lum gro-Baroth", 2, { canKick: true, canRank: true }),
  ],
});

// The same roll as Lum, an Associate, who may only read it and leave.
const asAssociate = () => {
  const card = asChampion();
  return {
    ...card,
    rank: 2,
    canInvite: false,
    canAddRank: false,
    grantable: [],
    ranks: card.ranks.map((each) => rank(each.number, each.name, each.kind, each.permissions)),
    members: card.members.map((each) => member(each.id, each.name, each.rank, { self: each.id === 5 })),
  };
};

const state = (faction, invite = null) =>
  JSON.stringify({
    tab: "faction",
    character: { firstName: "Modryn", lastName: "Oreyn", createdAt: FOUNDED, playedSeconds: 60, gold: 900 },
    account: { nickname: "Toyer", createdAt: FOUNDED, previousVisitAt: null, sanctions: [] },
    help: [],
    dice: { set: "imperial", sets: ["imperial"], recent: [], maxDice: 5 },
    lore: { namePublic: true, header: "Modryn Oreyn", preview: { header: "Modryn Oreyn", level: "public", fields: [] }, known: [] },
    faction: { fee: 500, invite, faction },
    languages: ["en"],
    language: "en",
  });

describe("the menu's faction tab", () => {
  let game;
  let page;

  beforeEach(async () => ({ game, page } = await mountPage(App, createBridge)));

  const click = (selector) => page.find(selector).trigger("click");
  const type = (field, text) => page.find(`[data-field="${field}"]`).setValue(text);
  const submit = (field) => page.find(`[data-field="${field}"]`).element.form.dispatchEvent(new Event("submit", { cancelable: true }));
  const ran = () => game.readSent("MenuFaction").map(([line]) => line);

  it("offers a player in no faction to found one, for the fee the server names", async () => {
    await game.emit("State", state(null));
    expect(page.find('[role="tab"][aria-selected="true"]').attributes("data-tab")).toBe("faction");
    expect(page.find('[data-part="found"]').text()).toContain("Founding costs 500 gold");
    expect(page.find('[data-part="invite"]').exists()).toBe(false);
    expect(page.find('[data-part="roll"]').exists()).toBe(false);
  });

  it("refuses a name too short at its field and sends nothing", async () => {
    await game.emit("State", state(null));
    await type("name", "a".repeat(FACTION_NAME_MIN - 1));
    submit("name");
    await flushPromises();
    expect(ran()).toEqual([]);
    expect(page.find('[data-field="name"]').attributes("aria-invalid")).toBe("true");
    expect(page.find('[role="alert"]').text()).toBe("The name has 3 to 32 characters.");
  });

  it("founds under the name typed, without its spare spaces, and waits for the answer", async () => {
    await game.emit("State", state(null));
    await type("name", "  Mages   Guild ");
    submit("name");
    await flushPromises();
    expect(ran()).toEqual(["/faction create Mages Guild"]);
    expect(page.find(".menu__faction-answer").text()).toBe("Asking the server...");
  });

  it("keeps the name when the server refuses it, and says why", async () => {
    await game.emit("State", state(null));
    await type("name", "Fighters Guild");
    submit("name");
    await game.emit("Answer", "There is a faction called Fighters Guild already.", false);
    await game.emit("State", state(null));
    const answer = page.find(".menu__faction-answer");
    expect(answer.text()).toBe("There is a faction called Fighters Guild already.");
    expect(answer.attributes("role")).toBe("alert");
    expect(page.find('[data-field="name"]').element.value).toBe("Fighters Guild");
  });

  it("shows the faction once founded, with the server's answer over it", async () => {
    await game.emit("State", state(null));
    await type("name", "Mages Guild");
    submit("name");
    await game.emit("Answer", "You founded Mages Guild for 500 gold. You are its leader.", true);
    await game.emit("State", state(asChampion()));
    expect(page.find(".menu__faction-answer").attributes("role")).toBe("status");
    expect(page.find(".menu__faction-answer").text()).toContain("You founded Mages Guild");
    expect(page.find('[data-part="found"]').exists()).toBe(false);
    expect(page.find('[data-part="roll"]').exists()).toBe(true);
  });

  it("answers an invitation with Join or Decline", async () => {
    await game.emit("State", state(null, { faction: "Mages Guild", by: "Breton", leftMs: 240_000 }));
    expect(page.find('[data-part="invite"]').text()).toContain("Breton invites you to join Mages Guild.");
    await click('[data-action="accept"]');
    await click('[data-action="decline"]');
    expect(ran()).toEqual(["/faction accept", "/faction decline"]);
  });

  it("lists the roll with each member's rank, and who is in the game", async () => {
    await game.emit("State", state(asChampion()));
    const facts = page.find(".menu__facts").text();
    expect(facts).toContain("Fighters Guild");
    expect(facts).toContain("November 1, 2026");
    expect(facts).toContain("Champion");
    expect(facts).toContain("4, in the game: 2");
    const rows = page.findAll(".menu__faction-member");
    expect(rows.map((row) => row.find(".menu__faction-rank").text())).toEqual(["Master", "Champion", "Warder", "Associate"]);
    expect(rows[0].find(".menu__faction-seen").text()).toBe("In the game");
    expect(rows[2].find(".menu__faction-seen").text()).toBe("Last seen November 2, 2026");
    expect(rows[1].find(".menu__faction-you").text()).toBe("you");
    expect(page.find('[data-part="roll"] .menu__note').text()).toContain("press H");
  });

  it("offers Rank and Remove only on the members the server lets the reader act on", async () => {
    await game.emit("State", state(asChampion()));
    const acts = (id) => page.findAll(`[data-member="${id}"] [data-action]`).map((button) => button.attributes("data-action"));
    expect(acts(1)).toEqual([]);
    expect(acts(2)).toEqual([]);
    expect(acts(3)).toEqual(["rank", "kick"]);
    expect(acts(5)).toEqual(["rank", "kick"]);
  });

  it("gives a member one of the ranks the reader may give, but the one they hold", async () => {
    await game.emit("State", state(asChampion()));
    await click('[data-member="3"] [data-action="rank"]');
    expect(page.find('[data-member="3"] [data-action="rank"]').attributes("aria-expanded")).toBe("true");
    expect(page.findAll('[data-member="3"] [data-give]').map((chip) => chip.text())).toEqual(["Scribe", "Associate"]);
    await click('[data-member="3"] [data-give="5"]');
    expect(ran()).toEqual(["/faction rank #3 #5"]);
    expect(page.find('[data-member="3"] [data-give]').exists()).toBe(false);
  });

  it("removes a member on the second click only, and forgets the first after a moment", async () => {
    vi.useFakeTimers();
    try {
      await game.emit("State", state(asChampion()));
      await click('[data-member="5"] [data-action="kick"]');
      expect(page.find('[data-member="5"] [data-action="kick"]').text()).toBe("Sure?");
      expect(ran()).toEqual([]);
      vi.advanceTimersByTime(CONFIRM_MS);
      await flushPromises();
      expect(page.find('[data-member="5"] [data-action="kick"]').text()).toBe("Remove");
      await click('[data-member="5"] [data-action="kick"]');
      await click('[data-member="5"] [data-action="kick"]');
      expect(ran()).toEqual(["/faction kick #5"]);
    } finally {
      vi.useRealTimers();
    }
  });

  it("shows what each rank may do, as chips where the reader may change it", async () => {
    await game.emit("State", state(asChampion()));
    expect(page.find('[data-rank="1"]').text()).toContain("Leads the faction, with every permission.");
    expect(page.find('[data-rank="3"] .menu__faction-allows').text()).toBe("May: Invite, Manage ranks");
    expect(page.find('[data-rank="3"] [data-perm]').exists()).toBe(false);
    expect(page.find('[data-rank="2"]').text()).toContain("New members start here.");
    const chips = page.findAll('[data-rank="4"] [data-perm]');
    expect(chips.map((chip) => chip.attributes("aria-pressed"))).toEqual(["true", "false", "false"]);
    expect(chips.map((chip) => chip.element.disabled)).toEqual([false, true, false]);
  });

  it("turns a permission on or off by what the rank holds", async () => {
    await game.emit("State", state(asChampion()));
    await click('[data-rank="4"] [data-perm="Invite"]');
    await click('[data-rank="4"] [data-perm="Ranks"]');
    expect(ran()).toEqual(["/faction perm #4 invite off", "/faction perm #4 ranks on"]);
  });

  it("renames a rank in its own field, refusing a name too long before it is sent", async () => {
    await game.emit("State", state(asChampion()));
    await click('[data-rank="4"] [data-action="rename"]');
    expect(page.find('[data-field="rename"]').element.value).toBe("Warder");
    await type("rename", "a".repeat(FACTION_RANK_NAME_MAX + 1));
    submit("rename");
    await flushPromises();
    expect(page.find('[data-rank="4"] [role="alert"]').text()).toBe("A rank's name has 2 to 24 characters.");
    await type("rename", "Warder of the North");
    submit("rename");
    await flushPromises();
    expect(ran()).toEqual(["/faction rankname #4 Warder of the North"]);
    await game.emit("Answer", "The rank Warder is now called Warder of the North.", true);
    expect(page.find('[data-field="rename"]').exists()).toBe(false);
  });

  it("moves a rank, deletes one on the second click and adds one", async () => {
    await game.emit("State", state(asChampion()));
    expect(page.find('[data-rank="4"] [data-action="up"]').exists()).toBe(false);
    await click('[data-rank="5"] [data-action="up"]');
    await click('[data-rank="5"] [data-action="delete"]');
    expect(page.find('[data-rank="5"] [data-action="delete"]').text()).toBe("Sure?");
    await click('[data-rank="5"] [data-action="delete"]');
    await type("rank", "Journeyman");
    submit("rank");
    await flushPromises();
    expect(ran()).toEqual(["/faction rankmove #5 up", "/faction rankdel #5", "/faction rankadd Journeyman"]);
    expect(page.find('[data-rank="1"] [data-action]').exists()).toBe(false);
    expect(page.find('[data-rank="2"] [data-action="delete"]').exists()).toBe(false);
  });

  it("lets a member without permissions only read the roll and leave, on the second click", async () => {
    await game.emit("State", state(asAssociate()));
    expect(page.findAll('[data-part="roll"] [data-action], [data-part="ranks"] [data-action], [data-perm], [data-field]')).toHaveLength(0);
    expect(page.find('[data-part="roll"] .menu__note').exists()).toBe(false);
    expect(page.find('[data-rank="4"] .menu__faction-allows').text()).toBe("May: Invite");
    expect(page.find('[data-rank="5"] .menu__faction-allows').text()).toBe("No permissions.");
    await click('[data-action="leave"]');
    expect(ran()).toEqual([]);
    await click('[data-action="leave"]');
    expect(ran()).toEqual(["/faction leave"]);
  });

  it("tells the only leader why they cannot leave, and the last member what leaving does", async () => {
    await game.emit("State", state({ ...asChampion(), canLeave: false }));
    expect(page.find('[data-action="leave"]').exists()).toBe(false);
    expect(page.find('[data-part="leave"]').text()).toContain("You are the only leader.");

    const alone = asChampion();
    await game.emit("State", state({ ...alone, members: [alone.members[1]] }));
    expect(page.find('[data-part="leave"]').text()).toContain("leaving dissolves the faction");
    expect(page.find('[data-action="leave"]').exists()).toBe(true);
  });

  it("leaves Q, E and the arrows to a field, and still closes on Escape from it", async () => {
    await game.emit("State", state(null));
    const field = page.find('[data-field="name"]').element;
    field.focus();
    for (const [key, keyCode] of [["q", 81], ["e", 69], ["ArrowLeft", 37]]) {
      field.dispatchEvent(new KeyboardEvent("keydown", { key, keyCode, bubbles: true, cancelable: true }));
    }
    await flushPromises();
    expect(page.find('[role="tab"][aria-selected="true"]').attributes("data-tab")).toBe("faction");
    field.dispatchEvent(new KeyboardEvent("keydown", { key: "Escape", keyCode: 27, bubbles: true, cancelable: true }));
    expect(game.readSent("MenuClose")).toEqual([[]]);
  });

  it("forgets the last answer on another tab and when the menu closes", async () => {
    await game.emit("State", state(asChampion()));
    await game.emit("Answer", "Burz gro-Khash is now Scribe.", true);
    expect(page.find(".menu__faction-answer").exists()).toBe(true);
    await page.find('[data-tab="keys"]').trigger("click");
    await page.find('[data-tab="faction"]').trigger("click");
    expect(page.find(".menu__faction-answer").exists()).toBe(false);
    await game.emit("Answer", "Burz gro-Khash is now Scribe.", true);
    await game.emit("Closed");
    await game.emit("State", state(asChampion()));
    expect(page.find(".menu__faction-answer").exists()).toBe(false);
  });

  it("reads in Polish to a Polish player", async () => {
    await game.emit("Locale", "pl");
    await game.emit("State", state(asChampion()));
    expect(page.find('[role="tab"][aria-selected="true"]').text()).toBe("Frakcja");
    expect(page.find('[data-part="ranks"] .menu__heading').text()).toBe("Rangi");
    expect(page.find('[data-rank="4"] [data-perm="Kick"]').text()).toBe("Usuwanie członków");
  });
});
