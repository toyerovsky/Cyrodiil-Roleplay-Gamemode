// @vitest-environment node
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";
import { findPower, NEAREST_CAST_CM } from "../fishing/charge.js";
import { BAIT_PICTURES, findSpeciesPicture, listPictures, ROD_PICTURES } from "../shared/fishing/pictures.js";

const SRC = fileURLToPath(new URL("../../src/", import.meta.url));
const read = (file) => readFileSync(SRC + file, "utf8");
const readEnum = (file) => [...read(file).matchAll(/^\s{4}(\w+),?$/gm)].map(([, name]) => name);
const readFloat = (file, name) => Number(read(file).match(new RegExp(`const float ${name} = ([\\d.]+)f;`))[1]);
const MS_IN_SECOND = 1000;

describe("the fishing pictures", () => {
  it("paint every species the server knows", () => {
    const species = readEnum("CyrodiilRP.Server/Fishing/FishSpecies.cs");

    expect(species.length).toBeGreaterThan(0);
    expect(species.filter((name) => !findSpeciesPicture(name))).toEqual([]);
  });

  it("paint every rod tier and every bait but none", () => {
    expect(Object.keys(ROD_PICTURES).sort()).toEqual(readEnum("CyrodiilRP.Common/Fishing/RodTier.cs").sort());
    expect(Object.keys(BAIT_PICTURES).sort()).toEqual(readEnum("CyrodiilRP.Server/Fishing/BaitKind.cs").filter((kind) => kind !== "None").sort());
    expect([...Object.values(ROD_PICTURES), ...Object.values(BAIT_PICTURES)].filter((name) => !listPictures().includes(name))).toEqual([]);
  });
});

describe("the cast's power", () => {
  it("starts where the server's nearest cast is", () => {
    expect(NEAREST_CAST_CM).toBe(readFloat("CyrodiilRP.Common/Fishing/FishingReach.cs", "Nearest"));
  });

  it("runs the server's triangle wave: up to full, down to nothing, up again", () => {
    const upMs = readFloat("CyrodiilRP.Common/Fishing/FishingCharge.cs", "UpSeconds") * MS_IN_SECOND;

    expect(findPower(0, upMs)).toBe(0);
    expect(findPower(upMs / 4, upMs)).toBeCloseTo(0.25);
    expect(findPower(upMs, upMs)).toBeCloseTo(1);
    expect(findPower(upMs * 1.5, upMs)).toBeCloseTo(0.5);
    expect(findPower(upMs * 2, upMs)).toBeCloseTo(0);
    expect(findPower(upMs * 2.25, upMs)).toBeCloseTo(0.25);
  });
});
