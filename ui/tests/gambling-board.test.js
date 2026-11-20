import { flushPromises, mount } from "@vue/test-utils";
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import TableBoard from "../gambling/TableBoard.vue";

const scene = vi.hoisted(() => ({ calls: [], options: null, picked: 1 }));
vi.mock("../dice/board/scene.js", () => ({ createBoard(options) {
  scene.options = options;
  return {
    start: async () => {}, resize: () => {},
    playShared: async (...args) => scene.calls.push(["play", ...args]),
    select: (mask) => scene.calls.push(["select", mask]),
    pick: () => scene.picked,
    dispose: () => scene.calls.push(["dispose"]),
  };
} }));
const initial = { id: 1, match: 81, seat: 0, round: 1, replay: 0, kind: "Initial", indices: [0, 1], values: [3, 6], seed: 725, set: "imperial" };
const match = (changes = {}) => ({ id: 81, seat: 0, round: 1, replay: 0, throws: [initial], activeThrow: 1, throwElapsedMs: 400, animationMs: 3000, ...changes });
const props = (changes = {}) => ({ match: match(), seat: 0, label: "Marcus Varro", picked: 0, enabled: true,
  takeAnimation: vi.fn(() => false), receivedAt: Date.now(), sound: { wake: vi.fn(), hit: vi.fn() }, ...changes });

beforeEach(() => {
  scene.calls = [];
  scene.picked = 1;
  vi.stubGlobal("ResizeObserver", class { observe() {} disconnect() {} });
});
afterEach(() => { vi.unstubAllGlobals(); vi.useRealTimers(); });

it("reopens an active match at rest without replaying and uses the seat's board", async () => {
  const page = mount(TableBoard, { props: props() });
  await flushPromises();
  expect(scene.options.boardModel).toBe("board");
  expect(scene.calls.find(([name]) => name === "play").at(-1)).toBe(false);
  expect(page.attributes("data-phase")).toBe("rest");
});

it("starts a fresh seeded event at its server elapsed time and never replays on a pick relay", async () => {
  vi.useFakeTimers();
  const page = mount(TableBoard, { props: props({ takeAnimation: vi.fn(() => true), receivedAt: Date.now() - 200 }) });
  await flushPromises();
  const played = scene.calls.find(([name]) => name === "play");
  expect(played.slice(-3)).toEqual([600, 3000, true]);
  expect(page.attributes("data-phase")).toBe("flight");
  await page.setProps({ picked: 2, match: match({ throwElapsedMs: 900 }) });
  await flushPromises();
  expect(scene.calls.filter(([name]) => name === "play")).toHaveLength(1);
  expect(scene.calls.at(-1)).toEqual(["select", 2]);
  scene.options.onLanded();
  await flushPromises();
  expect(page.attributes("data-phase")).toBe("rest");
});

it("adds only a new partial event and reconstructs kept poses", async () => {
  const page = mount(TableBoard, { props: props({ takeAnimation: vi.fn(() => true) }) });
  await flushPromises();
  const first = scene.calls.find(([name]) => name === "play");
  const reroll = { ...initial, id: 3, kind: "Reroll", indices: [1], values: [2], seed: 913 };
  await page.setProps({ match: match({ throws: [initial, reroll], activeThrow: 3, throwElapsedMs: 0 }) });
  await flushPromises();
  const last = scene.calls.filter(([name]) => name === "play").at(-1);
  expect(last[1]).toEqual([3, 2]);
  expect(last[3].tracks[0]).toEqual([first[3].tracks[0].at(-1)]);
});

it("raycasts the own board, ignores empty canvas and disables opponent selection", async () => {
  const page = mount(TableBoard, { props: props() });
  await flushPromises();
  await page.find("canvas").trigger("click", { clientX: 20, clientY: 30 });
  expect(page.emitted("pick")).toEqual([[1]]);
  scene.picked = -1;
  await page.find("canvas").trigger("click");
  await page.setProps({ enabled: false });
  scene.picked = 0;
  await page.find("canvas").trigger("click");
  expect(page.emitted("pick")).toHaveLength(1);
  page.unmount();
  expect(scene.calls.at(-1)).toEqual(["dispose"]);
});
