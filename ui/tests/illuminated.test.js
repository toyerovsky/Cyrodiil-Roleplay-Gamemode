import { mount } from "@vue/test-utils";
import { describe, expect, it } from "vitest";
import { RpIlluminated } from "../rp/index.js";

describe("an illuminated text", () => {
  it("draws its first letter with the game's initial and keeps the whole text to read", () => {
    const text = mount(RpIlluminated, { props: { text: "Before you set foot in Cyrodiil." } });
    expect(text.get("img").attributes("src")).toMatch(/b\.webp/);
    expect(text.get("img").attributes("alt")).toBe("");
    expect(text.text()).toBe("Before you set foot in Cyrodiil.");
    expect(text.classes()).toContain("rp-illuminated--initial");
  });

  it("has an initial for every letter a to z, whatever its case", () => {
    for (const letter of "abcdefghijklmnopqrstuvwxyz") {
      const text = mount(RpIlluminated, { props: { text: `${letter.toUpperCase()}rest` } });
      expect(text.find("img").exists(), letter).toBe(true);
    }
  });

  it("can stand in a heading", () => {
    const text = mount(RpIlluminated, { props: { text: "Marcus Varro", tag: "span" } });
    expect(text.element.tagName).toBe("SPAN");
    expect(text.text()).toBe("Marcus Varro");
  });

  it("starts plainly with anything else", () => {
    const text = mount(RpIlluminated, { props: { text: "Żeby ruszyć w drogę." } });
    expect(text.find("img").exists()).toBe(false);
    expect(text.classes()).not.toContain("rp-illuminated--initial");
    expect(text.text()).toBe("Żeby ruszyć w drogę.");
  });
});
