const ANSWER_MS = 50;
const NEXT_MS = 300;

const LINE = { stage: "Aim", nibbles: 0, bites: 0, bend: 0, lifting: false, tired: 0, safeFrom: 0.35, safeTo: 0.75, strikeWindowMs: 1000, outcome: null };
const NOTHING = { lengthCm: 0, grams: 0, trophy: false };

// "": the line is in, the cast would land at the mark. #waiting: the float lies out. #nibble: a fish tests the bait.
// #bite: the strike's window. #landing, #tight, #slack: the rod's bend in, over and under the safe band. #caught: a
// trophy pike. #found: a pearl instead of a fish. #lost: the line broke.
const SCENARIOS = {
  "": { views: [LINE], anchor: [0.52, 0.47] },
  waiting: { views: [{ ...LINE, stage: "Waiting" }], anchor: [0.5, 0.45] },
  nibble: { views: [{ ...LINE, stage: "Waiting" }, { ...LINE, stage: "Waiting", nibbles: 1 }], anchor: [0.5, 0.45] },
  bite: { views: [{ ...LINE, stage: "Bite", nibbles: 2, bites: 1 }], anchor: [0.5, 0.45] },
  landing: { views: [{ ...LINE, stage: "Landing", bites: 1, bend: 0.56, lifting: true, tired: 0.4 }], anchor: [0.47, 0.46] },
  tight: { views: [{ ...LINE, stage: "Landing", bites: 1, bend: 0.9, lifting: true, tired: 0.6 }], anchor: [0.55, 0.44] },
  slack: { views: [{ ...LINE, stage: "Landing", bites: 1, bend: 0.08, tired: 0.2 }], anchor: [0.45, 0.47] },
  caught: {
    views: [{ ...LINE, stage: "Rest", bites: 1, tired: 1, outcome: { number: 1, kind: "Caught", name: "Pike", lengthCm: 84, grams: 4446, trophy: true } }],
    anchor: null,
  },
  found: { views: [{ ...LINE, stage: "Rest", bites: 1, tired: 1, outcome: { number: 1, kind: "Found", name: "Flawless Pearl", ...NOTHING } }], anchor: null },
  lost: { views: [{ ...LINE, stage: "Rest", bites: 1, outcome: { number: 1, kind: "LostSnapped", name: null, ...NOTHING } }], anchor: null },
};

export function pretendGame(readym) {
  const scenario = SCENARIOS[location.hash.slice(1)] ?? SCENARIOS[""];
  readym.onSend((message) => {
    if (message !== "FishingReady") return;

    scenario.views.forEach((view, index) => setTimeout(() => readym.emit("State", JSON.stringify(view)), ANSWER_MS + index * NEXT_MS));
    if (scenario.anchor) setTimeout(() => readym.emit("Anchor", scenario.anchor[0], scenario.anchor[1], true), ANSWER_MS);
  });
}
