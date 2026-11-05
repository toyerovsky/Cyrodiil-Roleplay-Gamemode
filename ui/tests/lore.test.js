import { flushPromises } from "@vue/test-utils";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ASK_TIMEOUT_MS } from "../lore/asker.js";
import App from "../lore/App.vue";
import { createBridge } from "../lore/bridge.js";
import { pickTexts } from "../shared/locale.js";
import { mountPage } from "./readym.js";

const t = pickTexts("en", "ui.lore.");
// The dice fly on the dice board's small board this long before the trait's number shows (lore/App.vue).
const ROLL_REVEAL_MS = 4200;
const VIS_DELAY_MS = 450;

const field = (id, part, label, type, changes = {}) => ({
  id, part, label, type, max: 60, vis: "private", locked: false, ownerOnly: false, hint: `${label}, in a word.`, choices: null, needs: null,
  group: null, ...changes,
});

const FIELDS = [
  field("title", "identity", "Title", "text", { max: 30 }),
  field("seenAs", "identity", "Seen as", "text", { vis: "public", locked: true }),
  field("race", "heritage", "Race", "catalog", { vis: "public" }),
  field("subtype", "heritage", "Kind", "catalog", { vis: "public", needs: "race" }),
  field("relationship", "life", "Relationship", "choice", { choices: [{ id: "unwed", name: "Unwed" }, { id: "wed", name: "Wed under Mara" }] }),
  field("history", "story", "History", "longText", { max: 2000 }),
  field("notes", "story", "Notes", "longText", { max: 2000, locked: true, ownerOnly: true }),
  field("trait.lazy", "traits", "Lazy", "trait", { max: 3, group: "Vices and habits" }),
  field("trait.loyal", "traits", "Loyal", "trait", { max: 3, group: "Heart" }),
];

const empty = (vis = "private") => ({ catalogId: null, text: null, shown: "", summary: null, number: null, fixed: false, rolls: null, vis, empty: true });
const filled = (changes) => ({ ...empty(), empty: false, ...changes });

function cardOf(changes = {}) {
  const card = Object.fromEntries(FIELDS.map((each) => [each.id, empty(each.vis)]));
  card.title = filled({ text: "Legion veteran", shown: "Legion veteran" });
  card.notes = filled({ text: "Owes Lucia 40 gold.", shown: "Owes Lucia 40 gold." });
  return { ...card, ...changes };
}

const loaded = (changes = {}) => ({
  ok: true, mode: "canon", readOnly: false, fields: FIELDS, card: cardOf(), raceHint: { id: "race.imperial", name: "Imperial" }, maxCard: 16000,
  used: 40, name: "Marcus Varro", namePublic: false, ...changes,
});

describe("the lore card", () => {
  let game;
  let page;

  beforeEach(async () => ({ game, page } = await mountPage(App, createBridge)));
  afterEach(() => vi.useRealTimers());

  const asked = () => game.readSent("LoreAsk").map(([id, json]) => ({ id, ...JSON.parse(json) }));
  const lastAsk = (op) => asked().filter((request) => request.op === op).at(-1);
  const answer = (op, body) => game.emit("Answer", lastAsk(op).id, JSON.stringify(body));
  const press = async (key, code = key, options = {}) => {
    (document.activeElement ?? document.body).dispatchEvent(new KeyboardEvent("keydown", { key, code, bubbles: true, cancelable: true, ...options }));
    await flushPromises();
  };
  // As the game sends a key: its Windows key code and no scan code, so no `code`.
  const pressInGame = async (keyCode) => {
    (document.activeElement ?? document.body).dispatchEvent(new KeyboardEvent("keydown", { key: "", keyCode, bubbles: true, cancelable: true }));
    await flushPromises();
  };
  const rowOf = (id) => page.find(`[role="option"][data-field="${id}"]`);
  const cursor = () => page.find(".lore-row--cursor").attributes("data-field");
  const status = () => page.find(".lore__status").text();
  const section = (id) => page.find(`[data-part="${id}"]`);
  const button = (action) => page.find(`.lore__detail [data-action="${action}"]`);

  async function openEditor(changes) {
    await game.emit("Answer", 0, JSON.stringify({ op: "edit" }));
    await answer("load", loaded(changes));
  }

  it("tells the game it is there and shows nothing until the server opens it", () => {
    expect(game.sent).toEqual([["LoreReady"]]);
    expect(page.find(".lore").exists()).toBe(false);
  });

  it("opens the editor on the server's push and loads the card, the name first among the fields", async () => {
    await game.emit("Answer", 0, JSON.stringify({ op: "edit" }));
    expect(asked()).toEqual([{ id: 1, op: "load" }]);
    expect(page.find(".lore").exists()).toBe(true);
    await answer("load", loaded());
    expect(page.find(".lore__name").text()).toBe("Marcus Varro");
    expect(page.findAll('[role="tab"]').map((tab) => tab.text())).toEqual(["Identity1", "Background", "Life", "Look", "Story1", "Status", "Traits", "Nearby"]);
    expect(page.findAll(".lore-row").map((row) => row.attributes("data-field"))).toEqual(["name", "title", "seenAs"]);
    expect(rowOf("name").find(".lore-vis").text()).toBe(t["vis.private"]);
    expect(document.activeElement.getAttribute("role")).toBe("listbox");
    expect(page.find(".lore__usage").text()).toBe("40 of 16,000 characters used");
  });

  it("moves through the fields with the arrows and the sections with the arrows, Q and E, and the mouse", async () => {
    await openEditor();
    await press("ArrowDown");
    expect(cursor()).toBe("title");
    await press("ArrowUp");
    await press("ArrowUp");
    expect(cursor()).toBe("seenAs");
    await press("ArrowRight");
    expect(section("heritage").attributes("aria-selected")).toBe("true");
    await press("e", "KeyE");
    expect(section("life").attributes("aria-selected")).toBe("true");
    await press("q", "KeyQ");
    await press("ArrowLeft");
    expect(section("identity").attributes("aria-selected")).toBe("true");
    await section("story").trigger("click");
    await rowOf("notes").trigger("click");
    expect(cursor()).toBe("notes");
    expect(page.find(".lore-detail__title").text()).toBe("Notes");
  });

  it("writes a line with Enter, saves it with Enter and shows what the server said", async () => {
    await openEditor();
    await press("ArrowDown");
    await press("Enter");
    const box = page.find(".lore-edit__box");
    expect(box.element.value).toBe("Legion veteran");
    expect(document.activeElement).toBe(box.element);
    expect(game.readSent("LoreTyping")).toEqual([[true]]);
    await box.setValue("Caravan guard");
    await press("Enter");
    expect(lastAsk("set")).toMatchObject({ field: "title", text: "Caravan guard" });
    await answer("set", { ok: true, message: "Title set.", card: cardOf({ title: filled({ text: "Caravan guard", shown: "Caravan guard" }) }), used: 39, namePublic: false });
    expect(page.find(".lore-edit").exists()).toBe(false);
    expect(rowOf("title").find(".lore-row__value").text()).toBe("Caravan guard");
    expect(game.readSent("LoreTyping")).toEqual([[true], [false]]);
    expect(status()).toBe("Title set.");
  });

  it("marks a refused text at its box and says why, keeping what was written", async () => {
    await openEditor();
    await rowOf("title").trigger("dblclick");
    await page.find(".lore-edit__box").setValue("A title far too long");
    await button("save").trigger("click");
    await answer("set", { ok: false, message: "Title takes at most 30 characters.", card: cardOf(), used: 40, namePublic: false });
    expect(page.find(".lore-edit__box").attributes("aria-invalid")).toBe("true");
    expect(page.find('[role="alert"]').text()).toBe("Title takes at most 30 characters.");
    expect(page.find(".lore-edit__box").element.value).toBe("A title far too long");
  });

  it("throws away a changed text only on a second Esc, and keeps Enter a new line in a long text", async () => {
    await openEditor();
    await section("story").trigger("click");
    await press("Enter");
    const box = page.find(".lore-edit__box--long");
    await box.setValue("Served twelve years.");
    await press("Enter");
    expect(lastAsk("set")).toBeUndefined();
    await press("Escape");
    expect(page.find(".lore-edit").exists()).toBe(true);
    expect(status()).toBe(t.discard_long);
    await press("Escape");
    expect(page.find(".lore-edit").exists()).toBe(false);
    expect(game.readSent("LoreClose")).toEqual([]);
  });

  it("saves a long text with Ctrl+Enter", async () => {
    await openEditor();
    await section("story").trigger("click");
    await press("Enter");
    await page.find(".lore-edit__box--long").setValue("Line one\nLine two");
    await press("Enter", "Enter", { ctrlKey: true });
    expect(lastAsk("set")).toMatchObject({ field: "history", text: "Line one\nLine two" });
  });

  it("turns a field Public or Private with Space, sending only the last of a few presses, and at once with a click", async () => {
    vi.useFakeTimers();
    await openEditor();
    await press("ArrowDown");
    await press(" ", "Space");
    expect(rowOf("title").find(".lore-vis").text()).toBe(t["vis.public"]);
    await press(" ", "Space");
    await press(" ", "Space");
    expect(lastAsk("vis")).toBeUndefined();
    vi.advanceTimersByTime(VIS_DELAY_MS);
    await flushPromises();
    expect(asked().filter((request) => request.op === "vis")).toEqual([expect.objectContaining({ field: "title", vis: "public" })]);

    await page.find('.lore-who [data-vis="private"]').trigger("click");
    expect(lastAsk("vis")).toMatchObject({ field: "title", vis: "private" });
  });

  it("makes the name public or private through namevis, with the keys and the mouse", async () => {
    await openEditor();
    await press("Enter");
    await press("Enter");
    await page.find('.lore-who [data-vis="public"]').trigger("click");
    expect(lastAsk("namevis")).toEqual(expect.objectContaining({ vis: "public" }));
    expect(rowOf("name").find(".lore-vis").text()).toBe(t["vis.public"]);
    await answer("namevis", { ok: true, message: "Everyone sees your name now.", card: cardOf(), used: 40, namePublic: true });
    expect(page.find(".lore-detail__help").text()).toBe(t["vis_help.name_public"]);
  });

  it("keeps the notes for their owner: no Public or Private to pick, and Space says why", async () => {
    await openEditor();
    await section("story").trigger("click");
    await rowOf("notes").trigger("click");
    expect(rowOf("notes").find(".lore-vis").text()).toBe(t["vis.owner"]);
    expect(page.find(".lore-who").findAll("button")).toHaveLength(0);
    await press(" ", "Space");
    expect(status()).toBe("Who sees Notes cannot be changed.");
    expect(lastAsk("vis")).toBeUndefined();
  });

  it("clears a field on the second Delete or the second click of Clear", async () => {
    await openEditor();
    await press("ArrowDown");
    await press("Delete");
    expect(lastAsk("clear")).toBeUndefined();
    expect(status()).toBe("Press Delete or click Clear again to clear Title.");
    expect(button("clear").text()).toBe(t.clear_again);
    await press("Delete");
    expect(lastAsk("clear")).toMatchObject({ field: "title" });
    await answer("clear", { ok: true, message: "Title cleared.", card: cardOf({ title: empty() }), used: 26, namePublic: false });

    await section("story").trigger("click");
    await rowOf("notes").trigger("click");
    await button("clear").trigger("click");
    await button("clear").trigger("click");
    expect(lastAsk("clear")).toMatchObject({ field: "notes" });
  });

  it("picks from the lore list: the game's race offered first, a search, the arrows and Enter", async () => {
    await openEditor();
    await section("heritage").trigger("click");
    await press("Enter");
    expect(lastAsk("options")).toMatchObject({ field: "race" });
    expect(page.find(".lore-pick__list").text()).toBe(t.loading_list);
    const option = (id, name) => ({ group: null, id, name, summary: `${name} of Tamriel.`, kind: "race", race: name, origin: false, historical: false });
    await answer("options", { ok: true, field: "race", options: [option("race.breton", "Breton"), option("race.imperial", "Imperial"), option("race.nord", "Nord")], message: null });
    expect(page.find(".lore-option--cursor").text()).toContain("Imperial");
    expect(page.find(".lore-option--cursor").text()).toContain(t["tag.in_game"]);
    expect(page.find(".lore-pick__summary").text()).toBe("Imperial of Tamriel.");

    await page.find(".lore-pick input").setValue("n");
    expect(page.findAll(".lore-option").map((each) => each.attributes("data-option"))).toEqual(["race.nord", "race.breton"]);
    await press("ArrowDown");
    expect(page.find(".lore-option--cursor").attributes("data-option")).toBe("race.breton");
    await press("Enter");
    expect(lastAsk("pick")).toMatchObject({ field: "race", id: "race.breton" });
  });

  it("picks with the mouse: a click shows the entry, Pick takes it", async () => {
    await openEditor();
    await section("heritage").trigger("click");
    await button("edit").trigger("click");
    await answer("options", {
      ok: true, field: "race", message: null,
      options: [{ group: null, id: "race.nord", name: "Nord", summary: "From Skyrim.", kind: "race", race: "Nord", origin: false, historical: false }],
    });
    await page.find('[data-option="race.nord"]').trigger("click");
    await button("pick").trigger("click");
    expect(lastAsk("pick")).toMatchObject({ id: "race.nord" });
  });

  it("asks for the race before the kind it decides", async () => {
    await openEditor();
    await section("heritage").trigger("click");
    await rowOf("subtype").trigger("click");
    expect(page.find(".lore-detail__game").text()).toBe("Pick this first: Race. It decides what fits here.");
    expect(button("edit").attributes("disabled")).toBeDefined();
    await press("Enter");
    expect(lastAsk("options")).toBeUndefined();
    expect(status()).toBe("Pick this first: Race. It decides what fits here.");
  });

  it("sets a choice with a click on its chip", async () => {
    await openEditor();
    await section("life").trigger("click");
    await page.find('[data-choice="wed"]').trigger("click");
    expect(lastAsk("set")).toMatchObject({ field: "relationship", text: "wed" });
  });

  it("rolls a trait with R, shows the number once the dice have landed, and keeps it with Enter", async () => {
    vi.useFakeTimers();
    await openEditor();
    await section("traits").trigger("click");
    expect(page.find(".lore-list__group").text()).toBe("Vices and habits");
    await press("r", "KeyR");
    expect(lastAsk("roll")).toMatchObject({ field: "trait.lazy" });
    expect(status()).toBe("Rolling 5d20 for Lazy...");
    const rolled = filled({ number: 50, fixed: false, rolls: [{ dice: [6, 1, 17, 11, 15], total: 50 }], shown: "50" });
    await answer("roll", { ok: true, message: "Lazy: rolled 50.", card: cardOf({ "trait.lazy": rolled }), used: 40, roll: { dice: [6, 1, 17, 11, 15], total: 50, reroll: false }, namePublic: false });
    expect(page.find(".lore-trait-big--rolling").exists()).toBe(true);
    vi.advanceTimersByTime(ROLL_REVEAL_MS);
    await flushPromises();
    expect(page.find(".lore-trait-big__number").text()).toBe("50");
    expect(page.find(".lore-trait-big__words").text()).toBe("notably lazy");
    expect(page.find(".lore-detail__rolls").text()).toBe("Rolled: 6 + 1 + 17 + 11 + 15 = 50");
    expect(button("keep").exists()).toBe(true);
    await press("Enter");
    expect(lastAsk("keep")).toMatchObject({ field: "trait.lazy" });
  });

  it("types a trait's value once, digits only, and refuses to clear it", async () => {
    await openEditor();
    await section("traits").trigger("click");
    await button("type").trigger("click");
    const box = page.find(".lore-edit__box--number");
    await box.setValue("4x2");
    expect(box.element.value).toBe("42");
    await press("Enter");
    expect(lastAsk("set")).toMatchObject({ field: "trait.lazy", text: "42" });
    await answer("set", { ok: true, message: "Lazy is 42, set for good.", card: cardOf({ "trait.lazy": filled({ number: 42, fixed: true, shown: "42" }) }), used: 40, namePublic: false });
    expect(page.find(".lore-detail__fixed").text()).toBe(t["trait.fixed_typed"]);
    await press("Delete");
    expect(status()).toBe("Lazy is set for good. Only the staff can change a trait.");
  });

  it("previews the card as a stranger sees it with P, and goes back with Esc", async () => {
    await openEditor();
    await press("p", "KeyP");
    expect(lastAsk("preview")).toMatchObject({ level: "public" });
    await answer("preview", { ok: true, view: { header: "Imperial", level: "public", fields: [{ id: "race", part: "heritage", label: "Race", text: "Imperial", long: false }] } });
    expect(page.find(".lore-view__header").text()).toBe("Imperial");
    expect(page.find(".lore-view__heading").text()).toBe("Background");
    await press("p", "KeyP");
    expect(lastAsk("preview")).toMatchObject({ level: "private" });
    expect(page.find('[data-viewer="private"]').attributes("aria-pressed")).toBe("true");
    await page.find('[data-viewer="public"]').trigger("click");
    expect(lastAsk("preview")).toMatchObject({ level: "public" });
    await press("Escape");
    expect(page.find(".lore-view").exists()).toBe(false);
    expect(game.readSent("LoreClose")).toEqual([]);
  });

  const PLAYERS = [
    { id: "9", header: "Lucia Gallus", meters: 8.4, relation: "known", hasCard: true, incoming: null, outgoing: null },
    { id: "12", header: "Dunmer", meters: 15, relation: "stranger", hasCard: true, incoming: "introduce", outgoing: null },
  ];

  it("lists the players around and answers an introduction with = as the game sends it", async () => {
    await openEditor();
    await section("nearby").trigger("click");
    await answer("nearby", { ok: true, players: PLAYERS, range: 20 });
    expect(page.findAll(".lore-row--person").map((row) => row.find(".lore-row__label").text())).toEqual(["Lucia Gallus", "Dunmer"]);
    expect(section("nearby").find(".lore__count").text()).toBe("1");
    await press("ArrowDown");
    expect(page.find(".lore-detail__title").text()).toBe("Dunmer");
    await pressInGame(187);
    expect(lastAsk("accept")).toMatchObject({ player: "12" });
  });

  it("declines, and makes the player a stranger to someone who knows them on the second Delete", async () => {
    await openEditor();
    await section("nearby").trigger("click");
    await answer("nearby", { ok: true, players: PLAYERS, range: 20 });
    await page.find('[data-player="12"]').trigger("click");
    await button("decline").trigger("click");
    expect(lastAsk("decline")).toMatchObject({ player: "12" });
    await answer("decline", { ok: true, message: "Declined." });
    await answer("nearby", { ok: true, players: PLAYERS.slice(0, 1), range: 20 });
    await page.find('[data-player="9"]').trigger("click");
    expect(button("introduce").exists()).toBe(false);
    await pressInGame(46);
    expect(lastAsk("forget")).toBeUndefined();
    await pressInGame(46);
    expect(lastAsk("forget")).toMatchObject({ player: "9" });
  });

  it("reads a nearby player's card with Enter and goes back to Nearby with Esc", async () => {
    await openEditor();
    await section("nearby").trigger("click");
    await answer("nearby", { ok: true, players: PLAYERS, range: 20 });
    await press("Enter");
    expect(lastAsk("view")).toMatchObject({ player: "9" });
    await answer("view", { ok: true, id: "9", who: "Lucia Gallus", relation: "known", view: { header: "Lucia Gallus", level: "private", fields: [] } });
    expect(page.find(".rp-panel__title").text()).toBe("Lucia Gallus");
    expect(page.find('[data-action="back-nearby"]').exists()).toBe(true);
    await press("Escape");
    expect(page.find(".lore-row--person").exists()).toBe(true);
    expect(game.readSent("LoreClose")).toEqual([]);
  });

  it("shows another player's card the server pushes, and closes on Esc", async () => {
    await game.emit("Answer", 0, JSON.stringify({ op: "view", who: "Bosmer", view: null }));
    expect(page.find(".rp-panel__title").text()).toBe("Bosmer");
    expect(page.find(".lore-view__none").text()).toBe("Bosmer has no lore card yet.");
    await press("Escape");
    expect(game.readSent("LoreClose")).toEqual([[]]);
    await game.emit("Closed");
    expect(page.find(".lore").exists()).toBe(false);
  });

  it("leaves End to the game's bind, and closes on the close button", async () => {
    await openEditor();
    const end = new KeyboardEvent("keydown", { key: "End", code: "End", bubbles: true, cancelable: true });
    document.activeElement.dispatchEvent(end);
    expect(end.defaultPrevented).toBe(false);
    await page.find('[data-action="close"]').trigger("click");
    expect(game.readSent("LoreClose")).toEqual([[]]);
  });

  it("takes a held key for one press", async () => {
    await openEditor();
    document.activeElement.dispatchEvent(new KeyboardEvent("keydown", { key: "ArrowDown", code: "ArrowDown", repeat: true, bubbles: true }));
    await flushPromises();
    expect(cursor()).toBe("name");
  });

  it("asks again once when the server does not answer, then says so", async () => {
    vi.useFakeTimers();
    await game.emit("Answer", 0, JSON.stringify({ op: "edit" }));
    vi.advanceTimersByTime(ASK_TIMEOUT_MS);
    await flushPromises();
    expect(asked().map((request) => request.op)).toEqual(["load", "load"]);
    vi.advanceTimersByTime(ASK_TIMEOUT_MS);
    await flushPromises();
    expect(status()).toBe(t.no_answer);
  });

  it("refuses changes to a card the server cannot save", async () => {
    await openEditor({ readOnly: true });
    expect(status()).toBe(t.read_only);
    await press("ArrowDown");
    await press("Enter");
    expect(page.find(".lore-edit").exists()).toBe(false);
  });

  it("speaks Polish once the game says so, and leaves race names as they are", async () => {
    await openEditor();
    await game.emit("Locale", "pl");
    expect(section("heritage").text()).toBe(pickTexts("pl", "ui.lore.")["part.heritage"]);
    await section("nearby").trigger("click");
    await answer("nearby", { ok: true, players: PLAYERS, range: 20 });
    await press("ArrowDown");
    expect(page.find(".lore-detail__title").text()).toBe("Dunmer");
  });
});
