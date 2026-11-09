import { flushPromises } from "@vue/test-utils";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import App from "../admin/App.vue";
import { createBridge } from "../admin/bridge.js";
import { SEARCH_AFTER_MS } from "../admin/items.js";
import { MOST_ITEM_PIECES, REASON_MIN_LENGTH } from "../shared/limits.js";
import { mountPage } from "./readym.js";

const NOW = Date.UTC(2026, 9, 27, 12, 0);
const ACCOUNT = "3f2b9d4e-8a1c-4c6e-9b7a-1d2e3f4a5b6c";
const EVERYTHING = ["duty", "aheal", "arevive", "agoto", "abring", "aback", "atp", "afreeze", "aunfreeze", "amute", "aunmute", "awarn",
  "akick", "aban", "aunban", "agold", "aitem", "aroles", "atime", "aweather", "aspawn", "ahp", "amsg", "afeed"];
const SELF = 3;

const player = (number, name, changes = {}) => ({
  number, name, nickname: name.toLowerCase(), roles: [], onDuty: false, character: true, muted: false, frozen: false,
  area: "L_Tamriel", health: 80, healthMax: 80, ...changes,
});

const state = (changes = {}) =>
  JSON.stringify({
    tab: "players",
    focus: null,
    self: SELF,
    area: "L_Tamriel",
    acting: true,
    onDuty: true,
    tabs: ["players", "sanctions", "audit", "world"],
    allowed: EVERYTHING,
    players: [player(3, "Lucia", { muted: true }), player(4, "Brannoc", { frozen: true })],
    places: [{ id: "weye", name: "Weye", kind: "settlement" }, { id: "bravil", name: "Bravil", kind: "gate" }],
    weathers: ["clear", "rain"],
    traders: [
      { id: 1, shop: "general", area: "L_Tamriel", x: 100, y: -200, z: 300 },
      { id: 2, shop: "general", area: "L_ICMarketDistrict", x: 7, y: 8, z: 9 },
    ],
    spawn: { x: 400, y: -500, z: 600 },
    ...changes,
  });

const card = (number, changes = {}) =>
  JSON.stringify({
    number, name: "Brannoc", nickname: "brannoc", account: ACCOUNT, accountCreatedAt: NOW, previousVisitAt: null,
    characterCreatedAt: NOW, playedSeconds: 3725, area: "L_Tamriel", position: "1 2 3",
    sanctions: [], history: [{ id: 9, at: NOW, action: "admin.heal", actor: "Marcus Varro (Toyer)", target: null, details: null }],
    inventory: [{ id: "Lockpick", name: "Lockpick", type: "MISC", count: 12, worn: false }],
    ...changes,
  });

describe("the staff's panel", () => {
  let game;
  let page;

  beforeEach(async () => ({ game, page } = await mountPage(App, createBridge)));

  const press = async (key, code = key) => {
    (document.activeElement ?? document.body).dispatchEvent(new KeyboardEvent("keydown", { key, code, bubbles: true, cancelable: true }));
    await flushPromises();
  };
  // As the game sends a key: its Windows key code and no scan code, so no `code`.
  const pressInGame = async (keyCode, key) => {
    document.activeElement.dispatchEvent(new KeyboardEvent("keydown", { key, keyCode, bubbles: true, cancelable: true }));
    await flushPromises();
  };
  const selectedTab = () => page.find('[role="tab"][aria-selected="true"]').attributes("data-tab");
  const open = async (number) => {
    await page.find(`[data-player="${number}"]`).trigger("click");
    await game.emit("Part", "card", card(number));
  };
  const button = (label) => page.findAll("button").find((each) => each.text() === label);

  it("tells the game it is there, and shows nothing until the server sends the panel", () => {
    expect(game.sent).toEqual([["AdminReady"]]);
    expect(page.find(".admin").exists()).toBe(false);
  });

  it("shows only the tabs and the buttons the roles open", async () => {
    await game.emit("State", state({ tabs: ["players", "audit"], allowed: ["duty", "aheal"] }));
    expect(page.findAll('[role="tab"]').map((tab) => tab.attributes("data-tab"))).toEqual(["players", "audit"]);
    await open(4);
    expect(page.findAll(".admin-card__actions button").map((each) => each.attributes("data-action"))).toEqual(["heal"]);
    expect(page.find(".admin-items button").exists()).toBe(false);
  });

  it("off duty shows everything and runs nothing but the duty", async () => {
    await game.emit("State", state({ acting: false, onDuty: false }));
    expect(page.find(".admin__needed").exists()).toBe(true);
    expect(page.find(".admin__duty-state").text()).toBe("Off duty");
    await open(3);
    expect(page.find('[data-action="heal"]').attributes("disabled")).toBeDefined();

    await button("Go on duty").trigger("click");
    expect(game.readSent("AdminRun")).toEqual([["/duty"]]);
  });

  it("asks for the card of the player picked and offers the lift that fits", async () => {
    await game.emit("State", state());
    await open(4);
    expect(game.readSent("AdminAsk")).toEqual([["card", "4"]]);
    expect(page.find(".admin-card").text()).toContain(ACCOUNT);
    expect(page.find(".admin-card").text()).toContain("1 h 2 min");
    expect(page.find('[data-action="unfreeze"]').exists()).toBe(true);
    expect(page.find('[data-action="freeze"]').exists()).toBe(false);
    expect(page.find('[data-action="mute"]').exists()).toBe(true);
  });

  it("runs a button that needs nothing more at once and shows the answer", async () => {
    await game.emit("State", state());
    await open(3);
    await page.find('[data-action="heal"]').trigger("click");
    expect(game.readSent("AdminRun")).toEqual([["/aheal 3"]]);
    expect(page.find(".admin__status").text()).toBe("Asking the server...");

    await game.emit("Answer", "Healed Lucia.", true);
    expect(page.find(".admin__status").text()).toBe("Healed Lucia.");
    expect(page.find(".admin__status").classes()).toContain("admin__status--done");
    expect(game.readSent("AdminAsk").at(-1)).toEqual(["card", "3"]);
  });

  it("asks for a reason first, refuses a short one at its field, and runs a fair one", async () => {
    await game.emit("State", state());
    await open(3);
    await page.find('[data-action="warn"]').trigger("click");
    const dialog = page.find('[role="dialog"]');
    expect(dialog.text()).toContain("Warn: Lucia");

    await dialog.find("input").setValue("no");
    await dialog.trigger("submit");
    expect(page.find('[role="alert"]').text()).toContain(String(REASON_MIN_LENGTH));
    expect(dialog.find("input").attributes("aria-invalid")).toBe("true");
    expect(game.readSent("AdminRun")).toEqual([]);

    await dialog.find("input").setValue("  Shouting   over the wedding ");
    await dialog.trigger("submit");
    expect(game.readSent("AdminRun")).toEqual([["/awarn 3 Shouting over the wedding"]]);
    expect(page.find('[role="dialog"]').exists()).toBe(false);
  });

  it("checks the minutes of a mute before the reason", async () => {
    await game.emit("State", state());
    await open(4);
    await page.find('[data-action="mute"]').trigger("click");
    const inputs = page.findAll('[role="dialog"] input');
    await inputs[0].setValue("forever");
    await inputs[1].setValue("Spam in the market");
    await page.find('[role="dialog"]').trigger("submit");
    expect(page.find('[role="alert"]').text()).toContain("minutes");

    await inputs[0].setValue("30");
    await page.find('[role="dialog"]').trigger("submit");
    expect(game.readSent("AdminRun")).toEqual([["/amute 4 30 Spam in the market"]]);
  });

  it("closes the dialog on Escape first, then the panel", async () => {
    await game.emit("State", state());
    await open(3);
    await page.find('[data-action="kick"]').trigger("click");
    await press("Escape");
    expect(page.find('[role="dialog"]').exists()).toBe(false);
    expect(game.readSent("AdminClose")).toEqual([]);

    await press("Escape");
    expect(game.readSent("AdminClose")).toEqual([[]]);
    await game.emit("Closed");
    expect(page.find(".admin").exists()).toBe(false);
  });

  it("takes no more of an item than the player carries", async () => {
    await game.emit("State", state());
    await open(4);
    await page.find(".admin-items button").trigger("click");
    const input = page.find('[role="dialog"] input');
    await input.setValue("13");
    await page.find('[role="dialog"]').trigger("submit");
    expect(page.find('[role="alert"]').text()).toContain("12");

    await input.setValue("2");
    await page.find('[role="dialog"]').trigger("submit");
    expect(game.readSent("AdminRun")).toEqual([["/aitem take 4 Lockpick 2"]]);
  });

  it("looks items up from the second letter and gives the one picked", async () => {
    await game.emit("State", state());
    await open(4);
    const search = page.findAll(".admin-card input").at(-1);
    await search.setValue("l");
    expect(game.readSent("AdminAsk").filter(([kind]) => kind === "items")).toEqual([]);
    await search.setValue("lo");
    expect(game.readSent("AdminAsk").at(-1)).toEqual(["items", JSON.stringify({ text: "lo", type: "" })]);

    await game.emit("Part", "items", JSON.stringify([{ id: "Lockpick", name: "Lockpick", type: "MISC", count: 0, worn: false }]));
    await page.find(".admin-items--found button").trigger("click");
    await page.find('[role="dialog"]').trigger("submit");
    expect(game.readSent("AdminRun")).toEqual([["/aitem give 4 Lockpick 1"]]);
  });

  it("opens the card the server names", async () => {
    await game.emit("State", state({ focus: 4 }));
    expect(game.readSent("AdminAsk")).toEqual([["card", "4"]]);
  });

  it("lifts a sanction by the number while its player is online and by the account while away", async () => {
    await game.emit("State", state());
    await page.find('[data-tab="sanctions"]').trigger("click");
    expect(game.readSent("AdminAsk")).toEqual([["sanctions", ""]]);

    await game.emit("Part", "sanctions", JSON.stringify([
      { kind: "ban", account: ACCOUNT, nickname: "griefer", character: null, since: null, until: null, reason: "Griefing", by: null, online: null },
      { kind: "admin.mute", account: ACCOUNT, nickname: "lucia", character: "Lucia", since: NOW, until: NOW, reason: "Spam", by: "Toyer", online: 3 },
      { kind: "admin.freeze", account: ACCOUNT, nickname: "away", character: "Away", since: NOW, until: null, reason: "Running", by: "Toyer", online: null },
    ]));
    const lifts = page.findAll("tbody button");
    for (const lift of lifts) await lift.trigger("click");
    expect(game.readSent("AdminRun")).toEqual([[`/aunban ${ACCOUNT}`], ["/aunmute 3"], [`/aunfreeze ${ACCOUNT}`]]);
    expect(page.find("tbody").text()).toContain("Until lifted");
  });

  it("reads the audit a page at a time with the filters", async () => {
    await game.emit("State", state());
    await page.find('[data-tab="audit"]').trigger("click");
    expect(JSON.parse(game.readSent("AdminAsk")[0][1])).toEqual({ before: null });

    const entry = (id) => ({ id, at: NOW, action: "admin.heal", actor: "Toyer", target: "Lucia", details: null });
    await game.emit("Part", "audit", JSON.stringify({ entries: [entry(9), entry(8)], more: true, appended: false }));
    await page.find(".admin-audit__older").trigger("click");
    expect(JSON.parse(game.readSent("AdminAsk").at(-1)[1]).before).toBe(8);
    await game.emit("Part", "audit", JSON.stringify({ entries: [entry(7)], more: false, appended: true }));
    expect(page.findAll(".admin-entry")).toHaveLength(3);
    expect(page.find(".admin-audit__older").exists()).toBe(false);

    const inputs = page.findAll(".admin-audit__filter input");
    await inputs[0].setValue("admin.");
    await inputs[1].setValue("Toyer");
    await page.find(".admin-audit__filter").trigger("submit");
    expect(JSON.parse(game.readSent("AdminAsk").at(-1)[1])).toMatchObject({ action: "admin.", actor: "Toyer", target: null, before: null });
  });

  it("reads the roll log a page at a time, by a character's name, in the dice's own words", async () => {
    await game.emit("State", state({ tabs: ["players", "dice"] }));
    await page.find('[data-tab="dice"]').trigger("click");
    expect(game.readSent("AdminAsk")).toEqual([["rolls", JSON.stringify({ before: null })]]);

    const roll = (id, changes = {}) => ({
      id, at: NOW, character: "Lucia Gallus", asked: "1d20", dice: [{ sides: 20, value: 14 }], total: 14, reason: null, area: "L_Tamriel", ...changes,
    });
    const traits = { asked: "5d20", dice: [6, 1, 17, 11, 15].map((value) => ({ sides: 20, value })), total: 50, reason: "Lazy" };
    await game.emit("Part", "rolls", JSON.stringify({ rolls: [roll(9, traits), roll(8)], more: true, appended: false }));
    const rows = page.findAll(".admin-roll");
    expect(rows[0].text()).toContain("Lucia Gallus");
    expect(rows[0].find(".admin-roll__asked span").element.textContent).toBe("5d20 for Lazy");
    expect(rows[0].find(".admin-roll__total").text()).toBe("50");
    expect(rows[0].find(".admin-entry__details").text()).toBe("d20 [6], d20 [1], d20 [17], d20 [11], d20 [15] · L_Tamriel");
    expect(rows[1].find(".admin-entry__details").text()).toBe("L_Tamriel");

    await page.find(".admin-audit__older").trigger("click");
    expect(JSON.parse(game.readSent("AdminAsk").at(-1)[1]).before).toBe(8);
    await game.emit("Part", "rolls", JSON.stringify({ rolls: [roll(7)], more: false, appended: true }));
    expect(page.findAll(".admin-roll")).toHaveLength(3);
    expect(page.find(".admin-audit__older").exists()).toBe(false);

    await page.find(".admin-dice__filter input").setValue(" Lucia ");
    await page.find(".admin-dice__filter").trigger("submit");
    expect(JSON.parse(game.readSent("AdminAsk").at(-1)[1])).toEqual({ player: "Lucia", before: null });
    await game.emit("Part", "rolls", JSON.stringify({ rolls: [], more: false, appended: false }));
    expect(page.find(".admin-dice").text()).toContain("No rolls.");
  });

  it("clears the items found once the search is too short to look for", async () => {
    await game.emit("State", state());
    await open(4);
    const search = page.findAll(".admin-card input").at(-1);
    await search.setValue("lo");
    await game.emit("Part", "items", JSON.stringify([{ id: "Lockpick", name: "Lockpick", type: "MISC", count: 0, worn: false }]));
    expect(page.find(".admin-items--found").exists()).toBe(true);

    await search.setValue("l");
    expect(page.find(".admin-items--found").exists()).toBe(false);
  });

  it("offers no Take for an item the catalogue does not know, which /aitem cannot name", async () => {
    await game.emit("State", state());
    await page.find('[data-player="4"]').trigger("click");
    await game.emit("Part", "card", card(4, { inventory: [{ id: "0001A2B3", name: "0001A2B3", type: "", count: 1, worn: false }] }));
    expect(page.find(".admin-items button").exists()).toBe(false);
  });

  it("offers revive only for the dead, health only for the living, and nothing in the world to one away", async () => {
    await game.emit("State", state({
      players: [
        player(3, "Lucia"),
        player(4, "Brannoc", { health: 0 }),
        player(5, "Jzargo", { area: null, health: null, healthMax: null }),
      ],
    }));
    const actions = async (number) => {
      await open(number);
      return page.findAll(".admin-card__actions button").map((each) => each.attributes("data-action"));
    };
    expect(await actions(3)).toContain("health");
    expect(await actions(3)).not.toContain("revive");
    expect(await actions(4)).toContain("revive");
    expect(await actions(4)).not.toContain("health");
    const away = await actions(5);
    expect(away.filter((action) => ["goto", "bring", "back", "heal", "feed", "health", "revive"].includes(action))).toEqual([]);
    expect(away).toContain("message");
  });

  it("feeds a living player from the button beside heal, and offers it to nobody dead", async () => {
    await game.emit("State", state({ players: [player(3, "Lucia"), player(4, "Brannoc", { health: 0 })] }));
    await open(3);
    const actions = page.findAll(".admin-card__actions button").map((each) => each.attributes("data-action"));
    expect(actions.indexOf("feed")).toBe(actions.indexOf("heal") + 1);

    await page.find('[data-action="feed"]').trigger("click");
    expect(game.readSent("AdminRun")).toEqual([["/afeed 3"]]);

    await open(4);
    expect(page.find('[data-action="feed"]').exists()).toBe(false);
  });

  it("sets health within the character's most, and sends a message from the staff", async () => {
    await game.emit("State", state());
    await open(3);
    await page.find('[data-action="health"]').trigger("click");
    const health = page.find('[role="dialog"] input');
    await health.setValue("81");
    await page.find('[role="dialog"]').trigger("submit");
    expect(page.find('[role="alert"]').text()).toContain("80");
    await health.setValue("25");
    await page.find('[role="dialog"]').trigger("submit");

    await page.find('[data-action="message"]').trigger("click");
    await page.find('[role="dialog"]').trigger("submit");
    expect(page.find('[role="alert"]').exists()).toBe(true);
    await page.find('[role="dialog"] input').setValue("  Come to   the chapel ");
    await page.find('[role="dialog"]').trigger("submit");
    expect(game.readSent("AdminRun")).toEqual([["/ahp 3 25"], ["/amsg 3 Come to the chapel"]]);
  });

  it("starts the roles from the ones held and picks a ban's length with one click", async () => {
    await game.emit("State", state({ players: [player(3, "Lucia", { roles: ["moderator"] }), player(4, "Brannoc")] }));
    await open(3);
    await page.find('[data-action="roles"]').trigger("click");
    expect(page.find('[role="dialog"] input').element.value).toBe("moderator");
    expect(page.find('[role="dialog"]').text()).toContain("moderator, admin, owner");
    await page.find('[role="dialog"]').trigger("submit");

    await open(4);
    await page.find('[data-action="roles"]').trigger("click");
    expect(page.find('[role="dialog"] input').element.value).toBe("-");
    await page.find('[role="dialog"]').findAll("button")[0].trigger("click");

    await page.find('[data-action="ban"]').trigger("click");
    await page.find('[role="dialog"]').findAll(".rp-chip").find((chip) => chip.text() === "7 days").trigger("click");
    await page.findAll('[role="dialog"] input')[1].setValue("Griefing at the gate");
    await page.find('[role="dialog"]').trigger("submit");
    expect(game.readSent("AdminRun")).toEqual([["/aroles 3 moderator"], ["/aban 4 10080 Griefing at the gate"]]);
  });

  it("reads the audit again after a command, which wrote to it", async () => {
    await game.emit("State", state());
    await page.find('[data-tab="audit"]').trigger("click");
    const asked = game.readSent("AdminAsk").filter(([kind]) => kind === "audit").length;
    await game.emit("Answer", "Done.", true);
    expect(game.readSent("AdminAsk").filter(([kind]) => kind === "audit")).toHaveLength(asked + 1);
  });

  it("goes to coordinates, refusing ones that are not whole", async () => {
    await game.emit("State", state());
    await page.find('[data-tab="world"]').trigger("click");
    const axes = page.findAll(".admin-world__spot input");
    await axes[0].setValue("100");
    await axes[1].setValue("-2.5");
    await axes[2].setValue("300");
    await page.find(".admin-world__spot form").trigger("submit");
    expect(page.find(".admin-world__spot [role='alert']").exists()).toBe(true);
    await axes[1].setValue("-200");
    await page.find(".admin-world__spot form").trigger("submit");
    expect(game.readSent("AdminRun")).toEqual([["/atp 100 -200 300"]]);
  });

  it("offers Go only to a trader in the staff member's own area", async () => {
    await game.emit("State", state());
    await page.find('[data-tab="world"]').trigger("click");
    const rows = page.findAll(".admin-world__traders li");
    expect(rows[0].find("button").exists()).toBe(true);
    expect(rows[1].find("button").exists()).toBe(false);
    expect(rows[1].text()).toContain("In another area");
  });

  it("goes to a place, a trader and sets the sky of the area", async () => {
    await game.emit("State", state());
    await page.find('[data-tab="world"]').trigger("click");
    await page.find('[data-place="weye"]').trigger("click");
    await button("Rain").trigger("click");
    await page.find(".admin-world__traders button").trigger("click");
    await page.find(".admin-world__time input").setValue("13:30");
    await page.find(".admin-world__time").trigger("submit");
    expect(game.readSent("AdminRun")).toEqual([["/atp weye"], ["/aweather rain"], ["/atp 100 -200 300"], ["/atime 13:30"]]);
  });

  it("sets the spawn where the staff member stands and goes to it", async () => {
    await game.emit("State", state());
    await page.find('[data-tab="world"]').trigger("click");
    expect(page.find(".admin-world__spawn-at").text()).toBe("At 400 -500 600 cm");
    await button("Set it where I stand").trigger("click");
    await page.find(".admin-world__spawn").findAll("button").at(-1).trigger("click");
    expect(game.readSent("AdminRun")).toEqual([["/aspawn here"], ["/aspawn go"]]);
  });

  it("without a spawn says so and has nowhere to go", async () => {
    await game.emit("State", state({ spawn: null }));
    await page.find('[data-tab="world"]').trigger("click");
    const spawn = page.find(".admin-world__spawn");
    expect(spawn.text()).toContain("No spawn is set");
    expect(spawn.findAll("button").map((each) => each.text())).toEqual(["Set it where I stand"]);
  });

  it("opens with the tab focused, the panel named by it", async () => {
    await game.emit("State", state());
    expect(document.activeElement.dataset.tab).toBe("players");
    const panel = page.find('[role="tabpanel"]');
    expect(page.find('[role="tab"][aria-selected="true"]').attributes("aria-controls")).toBe(panel.attributes("id"));
    expect(panel.attributes("aria-labelledby")).toBe(document.activeElement.id);
  });

  it("goes round the tabs with the arrows and Q and E from the tab list, the focus with them", async () => {
    await game.emit("State", state());
    await press("ArrowDown");
    expect(selectedTab()).toBe("sanctions");
    await press("ArrowUp");
    await press("ArrowUp");
    expect(selectedTab()).toBe("world");
    await press("e", "KeyE");
    expect(selectedTab()).toBe("players");
    await pressInGame(81, "q");
    expect(selectedTab()).toBe("world");
    expect(document.activeElement.dataset.tab).toBe("world");
    expect(page.findAll('[role="tab"]').map((tab) => tab.attributes("tabindex"))).toEqual(["-1", "-1", "-1", "0"]);
  });

  it("leaves Q and E to a field being typed in", async () => {
    await game.emit("State", state());
    page.find(".admin-players input").element.focus();
    await press("e", "KeyE");
    await press("ArrowDown");
    expect(selectedTab()).toBe("players");
  });

  it("filters the players with a group of toggle buttons, the filter in use pressed", async () => {
    await game.emit("State", state());
    const group = page.find('.admin-players [role="group"]');
    const filters = () => group.findAll("button");
    expect(group.attributes("aria-label")).toBe("Show");
    expect(page.find('[role="radio"]').exists()).toBe(false);
    expect(filters().map((filter) => filter.attributes("aria-pressed"))).toEqual(["true", "false", "false", "false"]);

    await filters()[2].trigger("click");
    expect(filters().map((filter) => filter.attributes("aria-pressed"))).toEqual(["false", "false", "true", "false"]);
    expect(page.findAll("[data-player]").map((row) => row.attributes("data-player"))).toEqual(["3"]);
  });
});

describe("the items tab", () => {
  let game;
  let page;

  beforeEach(async () => {
    vi.useFakeTimers();
    ({ game, page } = await mountPage(App, createBridge));
    await game.emit("State", state({ tabs: ["players", "items", "audit"] }));
    await page.find('[data-tab="items"]').trigger("click");
  });

  afterEach(() => vi.useRealTimers());

  const lockpick = { id: "Lockpick", name: "Lockpick", type: "MISC", count: 0, worn: false };
  const asked = () => game.readSent("AdminAsk").filter(([kind]) => kind === "items").map(([, json]) => JSON.parse(json));
  const giveButton = () => page.find('.admin-stock__found [data-item="Lockpick"]');
  const findLockpick = async () => {
    await page.find("input.admin-stock__search").setValue("lock");
    await vi.advanceTimersByTimeAsync(SEARCH_AFTER_MS);
    await game.emit("Part", "items", JSON.stringify([lockpick]));
  };

  it("lists a kind alone, and a kind with a few letters, once the typing stops", async () => {
    await page.find('[data-type="MISC"]').trigger("click");
    expect(asked()).toEqual([]);
    await vi.advanceTimersByTimeAsync(SEARCH_AFTER_MS);
    expect(asked()).toEqual([{ text: "", type: "MISC" }]);

    await page.find("input.admin-stock__search").setValue("lo");
    await vi.advanceTimersByTimeAsync(SEARCH_AFTER_MS);
    expect(asked().at(-1)).toEqual({ text: "lo", type: "MISC" });
  });

  it("gives as many as picked to the staff member first, or to a player picked, and keeps it among the last given", async () => {
    expect(page.find(".admin-stock__target select").element.value).toBe(String(SELF));
    await findLockpick();

    await page.find('.admin-stock__count [role="group"]').findAll("button").find((chip) => chip.text() === "10").trigger("click");
    await giveButton().trigger("click");
    await page.find(".admin-stock__target select").setValue("4");
    await page.find(".admin-stock__recent button").trigger("click");
    expect(game.readSent("AdminRun")).toEqual([[`/aitem give ${SELF} Lockpick 10`], ["/aitem give 4 Lockpick 10"]]);
    expect(page.find(".admin-stock__recent").text()).toContain("Lockpick");
  });

  it("refuses a count the server would, at its field", async () => {
    await findLockpick();
    await page.find(".admin-stock__count input").setValue(String(MOST_ITEM_PIECES + 1));
    await giveButton().trigger("click");
    expect(page.find('[role="alert"]').text()).toContain(String(MOST_ITEM_PIECES));
    expect(page.find(".admin-stock__count input").attributes("aria-invalid")).toBe("true");
    expect(game.readSent("AdminRun")).toEqual([]);
  });
});

describe("the staff's panel and a lore card", () => {
  const LORE = {
    number: 4, name: "Brannoc", namePublic: false,
    view: {
      header: "Brannoc", level: "staff",
      fields: [
        { id: "race", part: "heritage", label: "Race", text: "Breton", long: false },
        { id: "notes", part: "story", label: "Notes", text: "Owes Glarthir 300 gold.", long: true },
      ],
    },
  };

  it("reads a player's lore card on request and clears a field on the second click, through /alore", async () => {
    const { game, page } = await mountPage(App, createBridge);
    await game.emit("State", state({ allowed: [...EVERYTHING, "alore"] }));
    await page.find('[data-player="4"]').trigger("click");
    await game.emit("Part", "card", card(4));
    expect(game.readSent("AdminAsk").filter(([kind]) => kind === "lore")).toEqual([]);

    await page.find('.admin-lore [data-action="lore"]').trigger("click");
    expect(game.readSent("AdminAsk").at(-1)).toEqual(["lore", "4"]);
    await game.emit("Part", "lore", JSON.stringify(LORE));
    expect(page.findAll(".admin-lore .lore-view__heading").map((heading) => heading.text())).toEqual(["Background", "Story"]);

    const clear = () => page.find('[data-clear="notes"]');
    await clear().trigger("click");
    expect(clear().text()).toBe("Clear it? Click again");
    expect(game.readSent("AdminRun")).toEqual([]);
    await clear().trigger("click");
    expect(game.readSent("AdminRun")).toEqual([["/alore 4 clear notes"]]);

    await game.emit("Answer", "Cleared Notes of Brannoc.", true);
    expect(game.readSent("AdminAsk").at(-1)).toEqual(["lore", "4"]);
  });

  it("offers no Clear to roles without /alore, and says when a player has no card", async () => {
    const { game, page } = await mountPage(App, createBridge);
    await game.emit("State", state());
    await page.find('[data-player="3"]').trigger("click");
    await game.emit("Part", "card", card(3));
    await page.find('.admin-lore [data-action="lore"]').trigger("click");
    await game.emit("Part", "lore", JSON.stringify({ ...LORE, number: 3 }));
    expect(page.find("[data-clear]").exists()).toBe(false);
    await game.emit("Part", "lore", JSON.stringify({ number: 3, name: "Lucia", namePublic: false, view: { header: "Lucia", level: "staff", fields: [] } }));
    expect(page.find(".admin-lore").text()).toContain("No lore card yet.");
  });
});
