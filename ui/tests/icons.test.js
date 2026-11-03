// @vitest-environment node
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";
import { ICONS } from "../rp/icons.js";

const WHEEL_ICONS = fileURLToPath(new URL("../../src/CyrodiilRP.Server/Interactions/WheelIcons.cs", import.meta.url));

describe("the icons", () => {
  it("are every one the server names, and only those", () => {
    const named = [...readFileSync(WHEEL_ICONS, "utf8").matchAll(/const string \w+ = "(\w+)";/g)].map(([, name]) => name);

    expect(named.length).toBeGreaterThan(0);
    expect(Object.keys(ICONS).sort()).toEqual([...named].sort());
  });
});
