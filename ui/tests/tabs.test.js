import { flushPromises, mount } from "@vue/test-utils";
import { describe, expect, it } from "vitest";
import { makeTabId, RpTabs } from "../rp/index.js";

const TABS = ["character", "account", "help"].map((id) => ({ id, medallion: `${id}.webp`, label: id.toUpperCase() }));

// A page holding the tabs, selected by v-model, as the menu and the staff's panel hold them.
function mountTabs(props = {}) {
  const tabs = mount(RpTabs, {
    props: {
      tabs: TABS,
      label: "Tabs",
      controls: "panel",
      modelValue: "character",
      "onUpdate:modelValue": (id) => tabs.setProps({ modelValue: id }),
      ...props,
    },
    attachTo: document.body,
  });
  return tabs;
}

const selected = (tabs) => tabs.find('[aria-selected="true"]').attributes("data-tab");

async function press(target, init) {
  target.dispatchEvent(new KeyboardEvent("keydown", { bubbles: true, cancelable: true, ...init }));
  await flushPromises();
}

describe("the tabs", () => {
  it("are a vertical tab list of the game's medallions, each tab naming the panel it controls", () => {
    const tabs = mountTabs();
    expect(tabs.attributes()).toMatchObject({ role: "tablist", "aria-orientation": "vertical", "aria-label": "Tabs" });
    const buttons = tabs.findAll('[role="tab"]');
    expect(buttons.map((button) => button.text())).toEqual(["CHARACTER", "ACCOUNT", "HELP"]);
    expect(buttons.map((button) => button.attributes("id"))).toEqual(TABS.map((each) => makeTabId("panel", each.id)));
    expect(buttons.every((button) => button.attributes("aria-controls") === "panel")).toBe(true);
    expect(tabs.find("img").attributes()).toMatchObject({ src: "character.webp", alt: "" });
  });

  it("keep only the selected tab in the order of Tab", () => {
    const tabs = mountTabs({ modelValue: "account" });
    expect(tabs.findAll('[role="tab"]').map((button) => [button.attributes("aria-selected"), button.attributes("tabindex")])).toEqual([
      ["false", "-1"],
      ["true", "0"],
      ["false", "-1"],
    ]);
  });

  it("select the tab clicked", async () => {
    const tabs = mountTabs();
    await tabs.find('[data-tab="help"]').trigger("click");
    expect(tabs.emitted("update:modelValue")).toEqual([["help"]]);
    expect(selected(tabs)).toBe("help");
  });

  it("go round on Q, E and the arrows from a tab, the focus with them", async () => {
    const tabs = mountTabs();
    tabs.find('[data-tab="character"]').element.focus();
    await press(document.activeElement, { key: "ArrowDown", code: "ArrowDown" });
    expect(selected(tabs)).toBe("account");
    await press(document.activeElement, { key: "e", code: "KeyE" });
    expect(selected(tabs)).toBe("help");
    await press(document.activeElement, { key: "ArrowRight", code: "ArrowRight" });
    expect(selected(tabs)).toBe("character");
    await press(document.activeElement, { key: "q", code: "KeyQ" });
    expect(selected(tabs)).toBe("help");
    await press(document.activeElement, { key: "ArrowLeft", code: "ArrowLeft" });
    expect(selected(tabs)).toBe("account");
    expect(document.activeElement.dataset.tab).toBe("account");
  });

  it("read the keys by their place, as the game sends them without a code", async () => {
    const tabs = mountTabs();
    tabs.find('[data-tab="character"]').element.focus();
    await press(document.activeElement, { key: "E", keyCode: 69 });
    expect(selected(tabs)).toBe("account");
    await press(document.activeElement, { key: "ArrowUp", keyCode: 38 });
    expect(selected(tabs)).toBe("character");
  });

  it("take a held key for one press, and leave the others alone", async () => {
    const tabs = mountTabs();
    tabs.find('[data-tab="character"]').element.focus();
    await press(document.activeElement, { key: "e", code: "KeyE", repeat: true });
    await press(document.activeElement, { key: "Tab", code: "Tab" });
    expect(tabs.emitted("update:modelValue")).toBeUndefined();
  });

  it("turn on a key a page hands on from anywhere, and say which keys are theirs", async () => {
    const tabs = mountTabs();
    const event = (code) => new KeyboardEvent("keydown", { code, cancelable: true });
    const turned = event("KeyE");
    expect(tabs.vm.takeKey(turned)).toBe(true);
    expect(turned.defaultPrevented).toBe(true);
    expect(tabs.vm.takeKey(event("Escape"))).toBe(false);
    await flushPromises();
    expect(selected(tabs)).toBe("account");
    expect(document.activeElement.dataset.tab).toBe("account");
  });

  it("come in two sizes", () => {
    expect(mountTabs().classes()).toContain("rp-tabs--large");
    expect(mountTabs({ size: "small" }).classes()).toContain("rp-tabs--small");
  });
});
