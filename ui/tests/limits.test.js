// @vitest-environment node
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";
import {
  CHAT_HISTORY_LINES,
  CHAT_MAX_LENGTH,
  MAX_MINUTES,
  MOST_ITEM_PIECES,
  NAME_MAX_LENGTH,
  NAME_MIN_LENGTH,
  REASON_MIN_LENGTH,
} from "../shared/limits.js";

const SRC = fileURLToPath(new URL("../../src/", import.meta.url));

function readConstant(file, name) {
  const match = readFileSync(SRC + file, "utf8").match(new RegExp(`const int ${name} = ([\\d_]+);`));
  if (!match) throw new Error(`${name} is not in ${file}`);
  return Number(match[1].replaceAll("_", ""));
}

describe("a page's limits", () => {
  it("are the server's", () => {
    expect(CHAT_MAX_LENGTH).toBe(readConstant("CyrodiilRP.Common/Chat/ChatLimits.cs", "MaxMessageLength"));
    expect(CHAT_HISTORY_LINES).toBe(readConstant("CyrodiilRP.Common/Chat/ChatLimits.cs", "HistoryLines"));
    expect(NAME_MIN_LENGTH).toBe(readConstant("CyrodiilRP.Server/Characters/NameRules.cs", "MinLength"));
    expect(NAME_MAX_LENGTH).toBe(readConstant("CyrodiilRP.Server/Characters/NameRules.cs", "MaxLength"));
    expect(REASON_MIN_LENGTH).toBe(readConstant("CyrodiilRP.Server/Commands/CommandCall.cs", "MinReasonLength"));
    expect(MAX_MINUTES).toBe(readConstant("CyrodiilRP.Server/Commands/CommandLine.cs", "MaxMinutes"));
    expect(MOST_ITEM_PIECES).toBe(readConstant("CyrodiilRP.Server/Admin/ItemCommands.cs", "MostPieces"));
  });
});
