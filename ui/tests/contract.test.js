// @vitest-environment node
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";
import { PAGES } from "../pages.js";

const UI = fileURLToPath(new URL("..", import.meta.url));
const CLIENT = fileURLToPath(new URL("../../src/CyrodiilRP.Client/", import.meta.url));

// [ReceiveFromCef] OnX(page, ...) is readym.sendX(...) on the page, [SendToCef] SendX(page, ...) is readym.on("X").
// The arguments after the page are counted for what the page sends.
function readDeclared(file) {
  const source = readFileSync(CLIENT + file, "utf8");
  const findMessages = (attribute, prefix) =>
    [...source.matchAll(new RegExp(`\\[${attribute}\\]\\s+[\\w\\s]*? void ${prefix}(\\w+)\\(BrowserHandle \\w+([^)]*)\\)`, "g"))]
      .map(([, name, rest]) => `${name}/${rest.split(",").filter((part) => part.trim()).length}`)
      .sort();
  return { receives: findMessages("ReceiveFromCef", "On"), sends: findMessages("SendToCef", "Send") };
}

function readUsed(file) {
  const source = readFileSync(UI + file, "utf8");
  const sends = [...source.matchAll(/readym\.send(\w+)\(([^)]*)\)/g)]
    .map(([, name, args]) => `${name}/${args.split(",").filter((part) => part.trim()).length}`)
    .sort();
  const listens = [...source.matchAll(/readym\.on\("(\w+)"/g)].map(([, name]) => name).sort();
  return { sends, listens };
}

describe("the messages between a page and the game", () => {
  for (const { name, game } of PAGES) {
    it(`${name} sends what ${game} receives and listens for what it sends`, () => {
      const { receives, sends } = readDeclared(game);
      const bridge = readUsed(`${name}/bridge.js`);

      expect(receives.length).toBeGreaterThan(0);
      expect(bridge.sends).toEqual(receives);
      expect(bridge.listens).toEqual(sends.map((message) => message.split("/")[0]));
    });
  }
});

// What every page sends through ui/shared/start.js rather than its own bridge, and the one class of the game it reaches.
const SHARED = [{ file: "shared/presence.js", game: "Away/PresenceReports.cs" }];

describe("the messages every page sends", () => {
  for (const { file, game } of SHARED) {
    it(`${file} sends what ${game} receives`, () => {
      expect(readUsed(file).sends).toEqual(readDeclared(game).receives);
    });
  }
});
