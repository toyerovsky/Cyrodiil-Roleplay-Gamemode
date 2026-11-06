import { flushPromises } from "@vue/test-utils";
import { beforeEach, describe, expect, it } from "vitest";
import App from "../admin/App.vue";
import { createBridge } from "../admin/bridge.js";
import { FACTION_NAME_MAX, REASON_MIN_LENGTH } from "../shared/limits.js";
import { mountPage } from "./readym.js";

const FOUNDED = Date.UTC(2026, 10, 1, 18, 0);
const DAY_MS = 24 * 3600 * 1000;

const state = (changes = {}) =>
  JSON.stringify({
    tab: "factions",
    focus: null,
    self: 1,
    area: "L_Tamriel",
    acting: true,
    onDuty: true,
    tabs: ["players", "audit", "dice", "factions"],
    allowed: ["duty", "afaction"],
    players: [],
    places: [],
    weathers: [],
    traders: [],
    spawn: null,
    ...changes,
  });

const LIST = [
  { id: 7, name: "Fighters Guild", members: 3, online: 1, leaders: ["Vilena Donton"], foundedAt: FOUNDED },
  { id: 9, name: "Mages Guild", members: 1, online: 0, leaders: [], foundedAt: FOUNDED },
];

const guild = (changes = {}) => ({
  id: 7,
  name: "Fighters Guild",
  foundedAt: FOUNDED,
  ranks: [
    { number: 1, name: "Master", kind: "Leader", permissions: [], members: 1 },
    { number: 3, name: "Warder", kind: "Custom", permissions: ["Invite", "Kick"], members: 1 },
    { number: 2, name: "Associate", kind: "Entry", permissions: [], members: 1 },
  ],
  members: [
    { id: 1, name: "Vilena Donton", rank: 1, leader: true, online: 12, lastSeenAt: FOUNDED },
    { id: 2, name: "Burz gro-Khash", rank: 3, leader: false, online: null, lastSeenAt: FOUNDED + DAY_MS },
    { id: 3, name: "Lum gro-Baroth", rank: 2, leader: false, online: null, lastSeenAt: FOUNDED + DAY_MS },
  ],
  ...changes,
});

describe("the panel's factions", () => {
  let game;
  let page;

  beforeEach(async () => ({ game, page } = await mountPage(App, createBridge)));

  const asked = () => game.readSent("AdminAsk");
  const ran = () => game.readSent("AdminRun").map(([line]) => line);
  const click = (selector) => page.find(selector).trigger("click");
  const openGuild = async (card = guild()) => {
    await game.emit("State", state());
    await game.emit("Part", "factions", JSON.stringify(LIST));
    await click('[data-faction="7"]');
    await game.emit("Part", "faction", JSON.stringify(card));
  };
  const submitDialog = async () => {
    page.find(".admin-dialog__box").element.dispatchEvent(new Event("submit", { cancelable: true }));
    await flushPromises();
  };

  it("asks for every faction as the tab opens and lists them, each with its members and leaders", async () => {
    await game.emit("State", state());
    expect(asked()).toEqual([["factions", ""]]);
    expect(page.find('.admin__content [role="status"]').text()).toBe("Reading...");
    await game.emit("Part", "factions", JSON.stringify(LIST));
    const rows = page.findAll(".admin-faction");
    expect(rows.map((row) => row.find(".admin-faction__name").text())).toEqual(["Fighters Guild", "Mages Guild"]);
    expect(rows[0].text()).toContain("#7");
    expect(rows[0].text()).toContain("Members: 3, in the game: 1");
    expect(rows[0].text()).toContain("Led by Vilena Donton");
    expect(rows[1].text()).toContain("No leader");
    expect(page.text()).toContain("Pick a faction");
  });

  it("says so when there is none, and when none fits what is looked for", async () => {
    await game.emit("State", state());
    await game.emit("Part", "factions", "[]");
    expect(page.find(".admin-factions__list .admin__note").text()).toBe("There are no factions yet.");
    await game.emit("Part", "factions", JSON.stringify(LIST));
    await page.find('input[type="search"]').setValue("thieves");
    expect(page.find(".admin-factions__list .admin__note").text()).toBe("No faction fits.");
    await page.find('input[type="search"]').setValue("#9");
    expect(page.findAll(".admin-faction").map((row) => row.attributes("data-faction"))).toEqual(["9"]);
    await page.find('input[type="search"]').setValue("fight");
    expect(page.findAll(".admin-faction").map((row) => row.attributes("data-faction"))).toEqual(["7"]);
  });

  it("asks for the faction picked and shows its ranks and its roll", async () => {
    await openGuild();
    expect(asked().at(-1)).toEqual(["faction", "7"]);
    expect(page.find('[data-faction="7"]').attributes("aria-pressed")).toBe("true");
    expect(page.find('[data-rank="1"]').text()).toContain("Every permission");
    expect(page.find('[data-rank="3"]').text()).toContain("Invite, Remove members");
    expect(page.find('[data-rank="2"]').text()).toContain("No permissions");
    const rows = page.findAll("[data-member]");
    expect(rows.map((row) => row.findAll("td")[1].text())).toEqual(["Master", "Warder", "Associate"]);
    expect(rows[0].text()).toContain("In the game, [12]");
    expect(rows[1].text()).toContain("Last seen Nov 2, 2026");
  });

  it("offers to make a leader of everyone but a leader, and to remove anyone, each at once", async () => {
    await openGuild();
    const acts = (id) => page.findAll(`[data-member="${id}"] [data-action]`).map((button) => button.attributes("data-action"));
    expect(acts(1)).toEqual(["factionRemove"]);
    expect(acts(2)).toEqual(["factionLead", "factionRemove"]);
    await click('[data-member="2"] [data-action="factionLead"]');
    await click('[data-member="3"] [data-action="factionRemove"]');
    expect(ran()).toEqual(["/afaction lead #7 #2", "/afaction remove #7 #3"]);
  });

  it("dissolves only with a reason, asked for in a dialog", async () => {
    await openGuild();
    await click('[data-action="factionDissolve"]');
    expect(page.find(".admin-dialog__title").text()).toBe("Dissolve: Fighters Guild");
    await page.find('[data-field="reason"]').setValue("a".repeat(REASON_MIN_LENGTH - 1));
    await submitDialog();
    expect(ran()).toEqual([]);
    expect(page.find(".admin-dialog__problem").text()).toContain("at least 3 characters");
    await page.find('[data-field="reason"]').setValue("Griefing the  market");
    await submitDialog();
    expect(ran()).toEqual(["/afaction dissolve #7 Griefing the market"]);
  });

  it("renames under a name of a length the server takes", async () => {
    await openGuild();
    await click('[data-action="factionRename"]');
    const field = page.find('[data-field="name"]');
    expect(field.attributes("maxlength")).toBe(String(FACTION_NAME_MAX));
    await field.setValue("ab");
    await submitDialog();
    expect(page.find(".admin-dialog__problem").text()).toBe("The name has 3 to 32 characters.");
    expect(field.attributes("aria-invalid")).toBe("true");
    await field.setValue("Blackwood Company");
    await submitDialog();
    expect(ran()).toEqual(["/afaction rename #7 Blackwood Company"]);
  });

  it("reads the list and the faction again after a command, and closes the card of one that is gone", async () => {
    await openGuild();
    await click('[data-member="3"] [data-action="factionRemove"]');
    await game.emit("Answer", "Lum gro-Baroth is removed from Fighters Guild.", true);
    expect(asked().slice(-2)).toEqual([["factions", ""], ["faction", "7"]]);
    expect(page.find(".admin__status").text()).toBe("Lum gro-Baroth is removed from Fighters Guild.");

    await game.emit("Part", "factions", JSON.stringify(LIST.slice(1)));
    await game.emit("Part", "faction", "7");
    expect(page.find("[data-card]").exists()).toBe(false);
    expect(page.text()).toContain("Pick a faction");
  });

  it("keeps the card of the faction picked when another one's answer arrives late", async () => {
    await openGuild();
    await game.emit("Part", "faction", JSON.stringify(guild({ id: 9, name: "Mages Guild" })));
    expect(page.find("[data-card]").attributes("data-card")).toBe("7");
    await game.emit("Part", "faction", "9");
    expect(page.find("[data-card]").attributes("data-card")).toBe("7");
  });

  it("shows a role that may not change the factions no button to change them", async () => {
    await game.emit("State", state({ allowed: ["duty"] }));
    await game.emit("Part", "factions", JSON.stringify(LIST));
    await click('[data-faction="7"]');
    await game.emit("Part", "faction", JSON.stringify(guild()));
    expect(page.findAll("[data-card] [data-action]")).toHaveLength(0);
    expect(page.findAll("[data-member]")).toHaveLength(3);
  });

  it("off duty shows the buttons and runs none of them", async () => {
    await game.emit("State", state({ acting: false, onDuty: false }));
    await game.emit("Part", "factions", JSON.stringify(LIST));
    await click('[data-faction="7"]');
    await game.emit("Part", "faction", JSON.stringify(guild()));
    const buttons = page.findAll("[data-card] [data-action]");
    expect(buttons.length).toBeGreaterThan(0);
    expect(buttons.every((button) => button.element.disabled)).toBe(true);
    await click('[data-action="factionDissolve"]');
    expect(page.find(".admin-dialog__box").exists()).toBe(false);
    expect(ran()).toEqual([]);
  });
});
