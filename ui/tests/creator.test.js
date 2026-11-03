import { flushPromises } from "@vue/test-utils";
import { beforeEach, describe, expect, it } from "vitest";
import App from "../creator/App.vue";
import { createBridge } from "../creator/bridge.js";
import { NAME_MAX_LENGTH, NAME_MIN_LENGTH } from "../shared/limits.js";
import { fill, pickTexts } from "../shared/locale.js";
import { mountPage } from "./readym.js";

const t = pickTexts("en", "ui.creator.");

describe("the new character form", () => {
  let game;
  let page;

  beforeEach(async () => {
    ({ game, page } = await mountPage(App, createBridge));
  });

  const show = () => game.emit("CreatorShown", true);
  const button = () => page.get("button[type=submit]");
  const field = (label) => page.get(`#${page.findAll("label").find((l) => l.text() === label).attributes("for")}`);

  async function writeName(first, last) {
    await field(t.first_name).setValue(first);
    await field(t.last_name).setValue(last);
  }

  it("tells the game it is there once mounted", () => {
    expect(game.sent).toEqual([["CreatorReady"]]);
  });

  it("is hidden until the game shows it", async () => {
    expect(page.get(".creator").classes()).not.toContain("creator--shown");
    await show();
    expect(page.get(".creator").classes()).toContain("creator--shown");
  });

  it("takes the keyboard in the first field once shown", async () => {
    await show();
    expect(document.activeElement).toBe(field(t.first_name).element);
  });

  it("keeps the button off until both names are in", async () => {
    await show();
    expect(button().attributes("disabled")).toBeDefined();
    await field(t.first_name).setValue("Marius");
    expect(button().attributes("disabled")).toBeDefined();
    await field(t.last_name).setValue("Varro");
    expect(button().attributes("disabled")).toBeUndefined();
  });

  it("presses the seal onto the letter once both names are in, and keeps it there while the server checks", async () => {
    await show();
    const seal = () => page.get(".creator__seal");
    expect(seal().classes()).not.toContain("creator__seal--pressed");
    expect(seal().attributes("alt")).toBe("");
    await writeName("Marius", "Varro");
    expect(seal().classes()).toContain("creator__seal--pressed");
    await page.get("form").trigger("submit");
    expect(seal().classes()).toContain("creator__seal--pressed");
  });

  it("starts its lead with the illuminated initial and keeps the lead whole", () => {
    const lead = page.get(".creator__lead");
    expect(lead.find("img").exists()).toBe(true);
    expect(lead.text()).toBe(t.lead);
  });

  it("sends the trimmed name and waits for the answer", async () => {
    await show();
    await writeName("  marius ", "varro");
    await page.get("form").trigger("submit");

    expect(game.readSent("SubmitName")).toEqual([["marius", "varro"]]);
    expect(button().text()).toBe(t.checking);
    expect(button().attributes("disabled")).toBeDefined();
  });

  it("shows the server's refusal, marks the fields and lets the player try again", async () => {
    await show();
    await writeName("Marius", "Varro");
    await page.get("form").trigger("submit");

    await game.emit("NameAnswer", "Someone in Cyrodiil already goes by Marius Varro.");

    expect(page.get("[role=alert]").text()).toBe("Someone in Cyrodiil already goes by Marius Varro.");
    expect(field(t.first_name).attributes("aria-invalid")).toBe("true");
    expect(button().text()).toBe(t.submit);
    expect(button().attributes("disabled")).toBeUndefined();
    expect(document.activeElement).toBe(field(t.first_name).element);
  });

  it("does not send again on a held Enter", async () => {
    await show();
    await writeName("Marius", "Varro");
    const held = new KeyboardEvent("keydown", { key: "Enter", repeat: true, bubbles: true, cancelable: true });
    field(t.last_name).element.dispatchEvent(held);
    await flushPromises();

    expect(held.defaultPrevented).toBe(true);
  });

  it("names the server's limits in its hint, and the fields point to it", async () => {
    const hint = page.get(".creator__hint");
    expect(hint.text()).toBe(fill(t.hint, { min: NAME_MIN_LENGTH, max: NAME_MAX_LENGTH }));
    expect(field(t.first_name).attributes("aria-describedby")).toContain(hint.attributes("id"));
    expect(field(t.first_name).attributes("maxlength")).toBe(String(NAME_MAX_LENGTH));
  });
});
