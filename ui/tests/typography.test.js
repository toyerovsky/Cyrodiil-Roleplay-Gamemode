// @vitest-environment node
import { readdirSync, readFileSync } from "node:fs";
import { join, relative } from "node:path";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";

const UI = fileURLToPath(new URL("..", import.meta.url));
const SKIPPED = new Set(["node_modules", "dist", "tests", "test-results", "playwright-report"]);
const STYLE_FILE = /\.(css|vue)$/;
// The font shorthand resets font-feature-settings, and with it the full-height figures rp/base.css gives the body.
// "font: inherit" keeps them.
const SHORTHAND = /(^|[;{\s])font\s*:(?!\s*inherit\b)[^;}\n]+/g;

function listStyleFiles(folder) {
  return readdirSync(folder, { withFileTypes: true }).flatMap((entry) => {
    if (SKIPPED.has(entry.name)) return [];
    const path = join(folder, entry.name);
    if (entry.isDirectory()) return listStyleFiles(path);
    return STYLE_FILE.test(entry.name) ? [path] : [];
  });
}

const findShorthands = (text) => [...text.matchAll(SHORTHAND)].map((match) => match[0].trim().replace(/^[;{]\s*/, ""));

describe("type", () => {
  it("no style uses the font shorthand", () => {
    const found = listStyleFiles(UI).flatMap((file) => findShorthands(readFileSync(file, "utf8")).map((rule) => `${relative(UI, file)}: ${rule}`));
    expect(found).toEqual([]);
  });

  it("the check finds a shorthand but not its longhands or inherit", () => {
    expect(findShorthands(".a { font: 700 17px var(--font-body); }")).toEqual(["font: 700 17px var(--font-body)"]);
    expect(findShorthands(".a { font-size: 17px; font-weight: 700; font: inherit; }")).toEqual([]);
  });
});
