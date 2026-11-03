// @vitest-environment node
import { readdirSync, readFileSync } from "node:fs";
import { join } from "node:path";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";
import texts from "../../locales/en/ui.menu.json";
import { KEYS, TABS } from "../menu/tabs.js";

const SRC = fileURLToPath(new URL("../../src/", import.meta.url));
const CLOSE_KEY = "Esc";

const read = (file) => readFileSync(SRC + file, "utf8");

function readSources(folder) {
  return readdirSync(SRC + folder, { recursive: true })
    .filter((file) => file.endsWith(".cs") && !/[\\/](bin|obj)[\\/]/.test(`/${file}`))
    .map((file) => readFileSync(join(SRC, folder, file), "utf8"))
    .join("\n");
}

describe("the menu and the server", () => {
  it("lists every key the client binds, and only those", () => {
    const bound = [...readSources("CyrodiilRP.Client").matchAll(/RegisterKeyBind\(Key\.(\w+)/g)].map(([, key]) => key).sort();
    const listed = KEYS.map(({ key }) => key).filter((key) => key !== CLOSE_KEY).sort();
    expect(listed).toEqual(bound);
  });

  it("has every tab the server opens it on", () => {
    const opened = [...read("CyrodiilRP.Server/PlayerMenu/MenuTabs.cs").matchAll(/const string \w+ = "(\w+)";/g)].map(([, id]) => id);
    expect(opened.length).toBeGreaterThan(0);
    expect(opened.filter((id) => !TABS.some((tab) => tab.id === id))).toEqual([]);
  });

  it("names every heading the server files commands under", () => {
    const enumBody = read("CyrodiilRP.Server/Commands/CommandCategory.cs").match(/enum CommandCategory\s*\{([^}]*)\}/)[1];
    const categories = [...enumBody.matchAll(/(\w+),/g)].map(([, name]) => name.toLowerCase());
    expect(categories.length).toBeGreaterThan(0);
    expect(categories.filter((category) => !texts[`ui.menu.category.${category}`])).toEqual([]);
  });
});
