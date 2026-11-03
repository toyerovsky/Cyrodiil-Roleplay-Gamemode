// @vitest-environment node
import { readdirSync, readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";
import { fill, pickTexts } from "../shared/locale.js";

const LOCALES = fileURLToPath(new URL("../../locales/", import.meta.url));
const PLACEHOLDER = /\{([A-Za-z_][A-Za-z0-9_]*)\}/g;

describe("page texts", () => {
  it("are a page's own group with the prefix cut off", () => {
    expect(pickTexts("en", "ui.creator.").title).toBe("New character");
    expect(Object.keys(pickTexts("en", "ui.chat."))).toContain("placeholder");
  });

  it("fall back to English for a locale the pages lack", () => {
    expect(pickTexts("xx", "ui.creator.").submit).toBe(pickTexts("en", "ui.creator.").submit);
  });

  it("fill their placeholders in one pass, so a value is never read as one", () => {
    expect(fill("{name} says: {text}", { name: "Marius", text: "{name}?" })).toBe("Marius says: {name}?");
    expect(fill("{missing} stays", {})).toBe("{missing} stays");
  });
});

describe("every locale file", () => {
  const locales = readdirSync(LOCALES, { withFileTypes: true }).filter((d) => d.isDirectory()).map((d) => d.name);
  const read = (locale, file) => JSON.parse(readFileSync(`${LOCALES}${locale}/${file}`, "utf8"));
  const listForms = (text) => (typeof text === "string" ? [text] : Object.values(text ?? {}));
  const findPlaceholders = (text) => [...new Set(listForms(text).flatMap((form) => [...form.matchAll(PLACEHOLDER)].map((m) => m[1])))].sort();

  for (const locale of locales) {
    for (const file of readdirSync(LOCALES + locale)) {
      it(`${locale}/${file} keeps to its group and has what English has`, () => {
        const group = file.replace(/\.json$/, "");
        const keys = Object.keys(read(locale, file));
        expect(keys.filter((key) => !key.startsWith(`${group}.`))).toEqual([]);
        expect(keys.sort()).toEqual(Object.keys(read("en", file)).sort());
      });

      it(`${locale}/${file} fills the same placeholders as English`, () => {
        const english = read("en", file);
        const differ = Object.entries(read(locale, file))
          .filter(([key, text]) => findPlaceholders(text).join() !== findPlaceholders(english[key]).join())
          .map(([key]) => key);
        expect(differ).toEqual([]);
      });
    }
  }
});
