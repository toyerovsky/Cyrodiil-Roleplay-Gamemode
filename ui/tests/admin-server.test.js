// @vitest-environment node
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";
import texts from "../../locales/en/ui.admin.json";
import { ACTIONS, LIFTS, PLAYER_ACTIONS } from "../admin/actions.js";
import { ITEM_TYPES } from "../admin/items.js";
import { PARTS, TABS } from "../admin/tabs.js";

const SRC = fileURLToPath(new URL("../../src/", import.meta.url));

const read = (file) => readFileSync(SRC + file, "utf8");

// StaffPermissions.cs: the name of each constant and the command it is.
const permissions = Object.fromEntries(
  [...read("CyrodiilRP.Data/Staff/StaffPermissions.cs").matchAll(/const string (\w+) = "(\w+)";/g)].map(([, name, command]) => [name, command]),
);

describe("the panel and the server", () => {
  it("runs only commands the server lets the panel run", () => {
    const list = read("CyrodiilRP.Server/Admin/AdminActions.cs").match(/Commands =\s*\[([^\]]*)\]/)[1];
    const accepted = [...list.matchAll(/StaffPermissions\.(\w+)/g)].map(([, name]) => permissions[name]);
    expect(accepted.length).toBeGreaterThan(0);
    expect(Object.values(ACTIONS).map(({ command }) => command).filter((command) => !accepted.includes(command))).toEqual([]);
  });

  it("offers every type of item the server's catalogue holds, each named", () => {
    const items = JSON.parse(read("CyrodiilRP.Server/Admin/items.json"));
    expect([...new Set(items.map((item) => item.type))].sort()).toEqual([...ITEM_TYPES].sort());
    expect(ITEM_TYPES.filter((type) => !texts[`ui.admin.items.type.${type}`])).toEqual([]);
  });

  it("has every tab the server names", () => {
    const named = [...read("CyrodiilRP.Server/Admin/AdminTabs.cs").matchAll(/const string \w+ = "(\w+)";/g)].map(([, id]) => id);
    expect(TABS.map((tab) => tab.id)).toEqual(named);
  });

  it("asks for the parts the server answers, by the names it gives them", () => {
    const named = [...read("CyrodiilRP.Server/Admin/AdminParts.cs").matchAll(/const string (\w+) = "(\w+)";/g)].map(([, name, part]) => [
      name.charAt(0).toLowerCase() + name.slice(1),
      part,
    ]);
    expect(named.length).toBeGreaterThan(0);
    expect(PARTS).toEqual(Object.fromEntries(named));
  });

  it("names every button, tab and kind of sanction", () => {
    const keys = [
      ...PLAYER_ACTIONS.map((action) => `action.${action}`),
      ...TABS.map((tab) => `tab.${tab.id}`),
      ...Object.keys(LIFTS).map((kind) => `kind.${kind}`),
    ];
    expect(keys.filter((key) => !texts[`ui.admin.${key}`])).toEqual([]);
  });

  it("names the sanctions by the keys the server stores them under", () => {
    const keys = [
      read("CyrodiilRP.Server/Sanctions/Mutes.cs").match(/MuteKey = "([\w.]+)"/)[1],
      read("CyrodiilRP.Server/Sanctions/Freezes.cs").match(/FreezeKey = "([\w.]+)"/)[1],
      read("CyrodiilRP.Data/Sanctions/SanctionRow.cs").match(/Ban = "(\w+)"/)[1],
    ];
    expect(Object.keys(LIFTS).sort()).toEqual(keys.sort());
  });
});
