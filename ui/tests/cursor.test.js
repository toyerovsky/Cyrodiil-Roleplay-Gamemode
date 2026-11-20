// @vitest-environment node
import { describe, expect, it } from "vitest";
import { watchGameCursor } from "../shared/cursor.js";

// The game calls the page's __readym_cursor with the cursor's place every tick, and once with (-1, -1) as it goes.
function watch(target = {}) {
  const reports = [];
  watchGameCursor((shown) => reports.push(shown), target);
  return { target, reports };
}

describe("the game's cursor as a page hears of it", () => {
  it("reports the cursor once when it comes and once when it goes", () => {
    const { target, reports } = watch();

    target.__readym_cursor(640, 360);
    target.__readym_cursor(641, 360);
    target.__readym_cursor(-1, -1);

    expect(reports).toEqual([true, false]);
  });

  it("takes a cursor at the page's corner for a cursor", () => {
    const { target, reports } = watch();

    target.__readym_cursor(0, 0);

    expect(reports).toEqual([true]);
  });

  it("says nothing while no cursor has come", () => {
    const { target, reports } = watch();

    target.__readym_cursor(-1, -1);

    expect(reports).toEqual([]);
  });

  it("still calls the platform's function, whether it was there before the page or comes after", () => {
    const before = [];
    const early = watch({ __readym_cursor: (x, y) => before.push([x, y]) });
    early.target.__readym_cursor(3, 4);
    expect(before).toEqual([[3, 4]]);

    const after = [];
    const late = watch();
    late.target.__readym_cursor = (x, y) => after.push([x, y]);
    late.target.__readym_cursor(5, 6);
    expect(after).toEqual([[5, 6]]);
    expect(late.reports).toEqual([true]);
  });
});
