// @vitest-environment node
import { describe, expect, it, vi } from "vitest";
import board from "../dice/models/board.json";
import d6 from "../dice/models/dice/d6.json";
import { DIE_SCALE, scaleModel } from "../dice/board/dice.js";
import { finishPartialThrow, planPartialThrow, topFace } from "../dice/board/physics.js";
import { createSeatPlanner } from "../gambling/table.js";

const model = scaleModel(d6, DIE_SCALE);
const models = Array(5).fill(model);
const initial = () => finishPartialThrow(planPartialThrow(models, board, 739, [0, 1, 2, 3, 4], []), [4, 4, 1, 2, 6], models, []);
const snapshot = (throwData) => throwData.tracks.map((track) => track.map(({ p, q }) => [p.toArray(), q.toArray()]));

describe("shared partial throws", () => {
  it("is deterministic for a seed even when the two machines have different CPU clocks", () => {
    const poses = initial().tracks.map((track) => track.at(-1));
    const run = () => finishPartialThrow(planPartialThrow(models, board, 831, [0, 4], poses), [6, 3], models, poses);
    const clock = vi.spyOn(performance, "now");
    clock.mockReturnValue(0);
    const first = snapshot(run());
    clock.mockImplementationOnce(() => 1000000);
    expect(snapshot(run())).toEqual(first);
    clock.mockRestore();
  });

  it("lands on the server values and keeps every unselected die at exactly its last pose", () => {
    const poses = initial().tracks.map((track) => track.at(-1));
    const before = poses.map(({ p, q }) => [p.toArray(), q.toArray()]);
    const result = finishPartialThrow(planPartialThrow(models, board, 831, [0, 4], poses), [6, 3], models, poses);
    expect(result.tracks.map((track) => topFace(model, track.at(-1).q).face.value)).toEqual([6, 4, 1, 2, 3]);
    for (const index of [1, 2, 3]) {
      expect(result.tracks[index]).toHaveLength(1);
      expect([result.tracks[index][0].p.toArray(), result.tracks[index][0].q.toArray()]).toEqual(before[index]);
    }
    expect(poses.map(({ p, q }) => [p.toArray(), q.toArray()])).toEqual(before);
  });

  it("uses kept dice as static obstacles in the simulated flight", () => {
    const poses = initial().tracks.map((track) => track.at(-1));
    const first = planPartialThrow(models, board, 932, [0, 4], poses);
    const distant = poses.map(({ p, q }) => ({ p: p.clone().setX(100), q: q.clone() }));
    const withoutObstacles = planPartialThrow(models, board, 932, [0, 4], distant);
    expect(snapshot(first.sim)).not.toEqual(snapshot(withoutObstacles.sim));
    expect(poses[1].p.x).not.toBe(100);
  });

  it("reconstructs a full throw and a partial reroll at rest without any playback", () => {
    const events = [
      { id: 1, kind: "Initial", seed: 739, set: "imperial", indices: [0, 1, 2, 3, 4], values: [4, 4, 1, 2, 6] },
      { id: 3, kind: "Reroll", seed: 831, set: "imperial", indices: [0, 4], values: [6, 3] },
    ];
    const live = createSeatPlanner();
    live(events.slice(0, 1));
    const landed = live(events);
    const reopened = createSeatPlanner()(structuredClone(events));
    expect(snapshot(reopened.data)).toEqual(snapshot(landed.data));
    expect(reopened.values).toEqual([6, 4, 1, 2, 3]);
  });

  it("uses two server dice for Favor and rejects invalid partial indices", () => {
    const two = models.slice(0, 2);
    const result = finishPartialThrow(planPartialThrow(two, board, 789, [0, 1], []), [2, 6], two, []);
    expect(result.landed).toEqual([2, 6]);
    expect(() => planPartialThrow(two, board, 1, [2], [])).toThrow();
    expect(() => planPartialThrow(two, board, 1, [0, 0], [])).toThrow();
  });
});
