// @vitest-environment node
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";
import { PICTURES } from "../guides/pictures.js";
import { buildGuide } from "../guides/pretend.js";

const TOPIC_FILE = fileURLToPath(new URL("../../src/CyrodiilRP.Data/Entities/GuideTopic.cs", import.meta.url));

function readTopics() {
  const source = readFileSync(TOPIC_FILE, "utf8");
  const body = source.slice(source.indexOf("enum GuideTopic"));
  return [...body.matchAll(/^\s{4}(\w+),$/gm)].map(([, name]) => name);
}

describe("the guides the server knows", () => {
  const topics = readTopics();

  it("are read from GuideTopic.cs", () => expect(topics).toContain("Welcome"));

  it("each has a picture, and no picture is left over", () => expect(Object.keys(PICTURES).sort()).toEqual([...topics].sort()));

  it("each reads whole in the preview, with no placeholder left unfilled", () => {
    for (const locale of ["en", "pl"]) {
      for (const topic of topics) {
        const guide = buildGuide(topic, "Use", locale);
        const texts = [guide.title, guide.purpose, ...guide.steps.map((step) => step.text), ...guide.facts.flatMap((fact) => [fact.label, fact.value])];
        expect(texts.every((text) => typeof text === "string" && text.length > 0 && !text.includes("{")), `${topic} in ${locale}`).toBe(true);
      }
    }
  });
});
