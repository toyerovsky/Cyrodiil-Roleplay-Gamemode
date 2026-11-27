const ANSWER_MS = 50;
const NEXT_MS = 300;
// FishingCharge.UpSeconds.
const CHARGE_UP_MS = 1100;

const ROD = { name: "Fishing Rod (Worn)", tier: "Basic", condition: "Worn", reachCm: 700 };
const DAMAGED_ROD = { ...ROD, name: "Fishing Rod (Damaged)", condition: "Damaged" };
const LINE = {
  stage: "Aim",
  nibbles: 0,
  bites: 0,
  strikeWindowMs: 1000,
  fight: null,
  twitch: null,
  rod: ROD,
  bait: { kind: "Worms", name: "Fishing Worms", count: 12 },
  skill: { level: 7, xp: 40, xpNext: 62 },
  outcome: null,
};
const FIGHT = { tension: 0.42, tensionWarn: 0.8, stamina: 0.7, distanceCm: 640, pull: "Left", lean: 1, reeling: false, slack: 0, feel: "Fair" };
const NOTHING = { species: null, name: null, lengthCm: 0, grams: 0, trophy: false, record: false, xp: 0, levelUp: null, rare: false, rod: null, baitLost: false, releaseXp: 0 };
const CALM = { count: 0, last: "None", calmInMs: 0, spent: false };
const waiting = (line = {}) => ({ ...LINE, stage: "Waiting", twitch: CALM, ...line });
const fighting = (fight, line = {}) => ({ ...LINE, stage: "Fight", bites: 1, fight: { ...FIGHT, ...fight }, ...line });
const resting = (outcome, line = {}) => ({ ...LINE, stage: "Rest", bites: 1, outcome: { number: 1, ...NOTHING, ...outcome }, ...line });
const lost = (kind, outcome = {}, line = {}) => resting({ kind, ...outcome }, line);

// "": the line is in, the cast would land at the mark. #charging: Space held, the power swings. #waiting, #nibble,
// #bite: the float in calm water, a fish testing the bait, the strike's window. #twitch: the bait twitched, which drew
// the fish, the water troubled for a while. #spooked: a twitch that made the fish bolt, the float pulled all the way in. #fight: a run to the left, the rod leant against it.
// #strain: leant with the run and reeling into it, the line in the red. #dive: a dive far out. #rest: a tired fish
// close in, reeled. #slack: the line gone slack in a run. #monster: a monster on the master rod. #caught: a trophy pike,
// which may be let go. #released: the pike let go. #rare: an Emperor's golden carp, a record and a level reached, which
// may be let go. #found: a pearl. #lostearly, #lostnibble, #lostlate,
// #snapped, #slipped, #runaway: each way a fish gets away, #snapped and #runaway costing the rod and the lure.
// #nobait: waiting with no bait on a damaged rod.
const SCENARIOS = {
  "": { views: [LINE], anchor: [0.52, 0.47] },
  charging: { views: [LINE], anchor: [0.52, 0.47], charge: true },
  waiting: { views: [waiting()], anchor: [0.5, 0.45] },
  nibble: { views: [waiting(), waiting({ nibbles: 1 })], anchor: [0.5, 0.45] },
  twitch: { views: [waiting(), waiting({ twitch: { count: 1, last: "Drew", calmInMs: 3500, spent: false } })], anchor: [0.5, 0.46] },
  spooked: { views: [waiting(), waiting({ twitch: { count: 1, last: "Spooked", calmInMs: 3500, spent: true } })], anchor: [0.5, 0.52] },
  bite: { views: [{ ...LINE, stage: "Bite", nibbles: 2, bites: 1 }], anchor: [0.5, 0.45] },
  fight: { views: [fighting({})], anchor: [0.47, 0.46] },
  strain: { views: [fighting({ pull: "Right", lean: 1, reeling: true, tension: 0.88, stamina: 0.62, feel: "Heavy", distanceCm: 910 })], anchor: [0.56, 0.45] },
  dive: { views: [fighting({ pull: "Away", lean: 0, tension: 0.55, stamina: 0.5, distanceCm: 1240 })], anchor: [0.5, 0.41] },
  rest: { views: [fighting({ pull: "Rest", lean: 0, reeling: true, tension: 0.3, stamina: 0.22, distanceCm: 210, feel: "Small" })], anchor: [0.49, 0.55] },
  slack: { views: [fighting({ pull: "Left", lean: 0, tension: 0.03, slack: 0.6, stamina: 0.8 })], anchor: [0.44, 0.47] },
  monster: {
    views: [
      fighting(
        { pull: "Right", lean: -1, tension: 0.74, stamina: 0.95, distanceCm: 1820, feel: "Monster" },
        { rod: { name: "Master Fishing Rod", tier: "Master", condition: "Good", reachCm: 1300 }, bait: { kind: "Lure", name: "Spoon Lure", count: 1 }, skill: { level: 52, xp: 118, xpNext: 332 } },
      ),
    ],
    anchor: [0.58, 0.43],
  },
  caught: { views: [resting({ kind: "Caught", species: "Pike", name: "Pike", lengthCm: 84, grams: 4446, trophy: true, xp: 31, releaseXp: 10 })], anchor: null },
  released: { views: [resting({ kind: "Released", species: "Pike", name: "Pike", lengthCm: 84, grams: 4446, trophy: true, xp: 41 })], anchor: null },
  rare: {
    views: [
      resting(
        { kind: "Caught", species: "GoldenCarp", name: "Emperor's Golden Carp", lengthCm: 88, grams: 11176, trophy: true, record: true, xp: 270, levelUp: 46, rare: true, releaseXp: 90 },
        { skill: { level: 46, xp: 12, xpNext: 296 } },
      ),
    ],
    anchor: null,
  },
  found: { views: [resting({ kind: "Found", name: "Flawless Pearl", xp: 3 })], anchor: null },
  lostearly: { views: [lost("LostEarly")], anchor: null },
  lostnibble: { views: [lost("LostNibble")], anchor: null },
  lostlate: { views: [lost("LostLate")], anchor: null },
  snapped: {
    views: [lost("LostSnapped", { species: "Pike", name: "Pike", rod: "Damaged", baitLost: true }, { bait: null, rod: DAMAGED_ROD })],
    anchor: null,
  },
  slipped: { views: [lost("LostSlack", { species: "Trout", name: "Colovian Trout" })], anchor: null },
  runaway: { views: [lost("LostRunaway", { species: "Longfin", name: "Abecean Longfin", rod: "Broken", baitLost: true })], anchor: null },
  nobait: { views: [waiting({ bait: null, rod: DAMAGED_ROD })], anchor: [0.5, 0.45] },
};

export function pretendGame(readym) {
  const scenario = SCENARIOS[location.hash.slice(1)] ?? SCENARIOS[""];
  readym.onSend((message) => {
    if (message !== "FishingReady") return;

    scenario.views.forEach((view, index) => setTimeout(() => readym.emit("State", JSON.stringify(view)), ANSWER_MS + index * NEXT_MS));
    if (scenario.anchor) setTimeout(() => readym.emit("Anchor", scenario.anchor[0], scenario.anchor[1], true), ANSWER_MS);
    if (scenario.charge) setTimeout(() => readym.emit("Charge", true, CHARGE_UP_MS), ANSWER_MS);
  });
}
