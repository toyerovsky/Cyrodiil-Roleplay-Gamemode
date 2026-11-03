import { mount } from "@vue/test-utils";
import { describe, expect, it } from "vitest";
import { RpChip } from "../rp/index.js";

describe("a chip", () => {
  it("is a toggle button when it has a pressed state, marked by it", async () => {
    const chip = mount(RpChip, { props: { pressed: false }, slots: { default: "Tools" } });
    expect(chip.element.tagName).toBe("BUTTON");
    expect(chip.attributes()).toMatchObject({ type: "button", "aria-pressed": "false" });
    expect(chip.classes()).not.toContain("rp-chip--on");

    await chip.setProps({ pressed: true });
    expect(chip.attributes("aria-pressed")).toBe("true");
    expect(chip.classes()).toContain("rp-chip--on");
  });

  it("is a plain button without one, which a click runs unless it is disabled", async () => {
    let clicks = 0;
    const chip = mount(RpChip, { attrs: { onClick: () => clicks++ }, slots: { default: "Rain" } });
    expect(chip.attributes("aria-pressed")).toBeUndefined();
    await chip.trigger("click");
    expect(clicks).toBe(1);

    await chip.setProps({ disabled: true });
    await chip.trigger("click");
    expect(clicks).toBe(1);
  });

  it("is a stop of Tab and no radio", () => {
    const chip = mount(RpChip, { props: { pressed: true }, slots: { default: "English" } });
    expect(chip.attributes("tabindex")).toBeUndefined();
    expect(chip.attributes("role")).toBeUndefined();
  });
});
