// @vitest-environment node
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";
import { KINDS } from "../asks/kinds.js";

describe("the invitations and the server", () => {
  it("knows every kind of ask the server makes", () => {
    const source = readFileSync(fileURLToPath(new URL("../../src/CyrodiilRP.Server/Players/AskKinds.cs", import.meta.url)), "utf8");
    const kinds = [...source.matchAll(/const string \w+ = "(\w+)";/g)].map(([, kind]) => kind);
    expect(kinds.length).toBeGreaterThan(0);
    expect(Object.keys(KINDS).sort()).toEqual(kinds.sort());
  });
});
