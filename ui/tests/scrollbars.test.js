// @vitest-environment node
import { readdirSync, readFileSync } from "node:fs";
import { join, relative } from "node:path";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";

const UI = fileURLToPath(new URL("..", import.meta.url));
const SKIPPED = new Set(["node_modules", "dist", "tests", "test-results", "playwright-report"]);
const SOURCE_FILE = /\.(css|vue|js)$/;
// Chromium drops rp/base.css's ::-webkit-scrollbar look on an element with either of these set. "none" hides the bar on
// purpose and stays allowed.
const OVERRIDES = [/scrollbar-(color|width)\s*:(?!\s*none\b)[^;}\n]+/g, /scrollbar(Color|Width)["']?\s*:(?!\s*["'`]none["'`])[^,;}\n]+/g];

function listSourceFiles(folder) {
  return readdirSync(folder, { withFileTypes: true }).flatMap((entry) => {
    if (SKIPPED.has(entry.name)) return [];
    const path = join(folder, entry.name);
    if (entry.isDirectory()) return listSourceFiles(path);
    return SOURCE_FILE.test(entry.name) ? [path] : [];
  });
}

const findOverrides = (text) => OVERRIDES.flatMap((pattern) => [...text.matchAll(pattern)].map((match) => match[0].trim()));

describe("scrollbars", () => {
  it("no page overrides the design system's", () => {
    const found = listSourceFiles(UI).flatMap((file) => findOverrides(readFileSync(file, "utf8")).map((override) => `${relative(UI, file)}: ${override}`));
    expect(found).toEqual([]);
  });

  it("the check finds an override in a stylesheet and a style object, but not none", () => {
    expect(findOverrides(".list { scrollbar-width: thin }")).toEqual(["scrollbar-width: thin"]);
    expect(findOverrides('const style = { scrollbarColor: "red", color: "red" };')).toEqual(['scrollbarColor: "red"']);
    expect(findOverrides(".hidden { scrollbar-width: none }")).toEqual([]);
  });
});
