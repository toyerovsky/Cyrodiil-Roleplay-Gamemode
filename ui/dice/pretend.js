const ANSWER_MS = 50;
const MINUTE_MS = 60_000;
const SETS = ["altmer", "argonian", "ayleid", "bosmer", "breton", "daedric", "dunmer", "imperial", "khajiit", "nord", "orc", "redguard"];
const KINDS = [
  ["d6", 6],
  ["d10", 10],
  ["d20", 20],
  ["d100", 100],
];
const MAX_DICE = 5;
const KEPT = 10;

// The d100 is ten-sided, 10 to 100.
const rollDie = (sides) => (sides === 100 ? 10 * (1 + Math.floor(Math.random() * 10)) : 1 + Math.floor(Math.random() * sides));

// Marius Varro at the board: a few rolls behind him. #empty: none yet. #refuse: the server refuses every throw.
// #present: the board shut, a trait roll of the lore card shown on the small board. #crit: every die lands on its top.
export function pretendGame(readym) {
  const scenario = location.hash.slice(1);
  const now = Date.now();
  let set = "imperial";
  const recent =
    scenario === "empty"
      ? []
      : [
          { at: now - 2 * MINUTE_MS, asked: "1d20", dice: [{ sides: 20, value: 20 }], total: 20, reason: null },
          { at: now - 6 * MINUTE_MS, asked: "2d6", dice: [{ sides: 6, value: 3 }, { sides: 6, value: 5 }], total: 8, reason: null },
          {
            at: now - 14 * MINUTE_MS,
            asked: "5d20",
            dice: [6, 1, 17, 11, 15].map((value) => ({ sides: 20, value })),
            total: 50,
            reason: "Lazy",
          },
          { at: now - 31 * MINUTE_MS, asked: "1d20", dice: [{ sides: 20, value: 1 }], total: 1, reason: null },
        ];

  const view = () => ({ set, sets: SETS, recent: recent.slice(0, KEPT), maxDice: MAX_DICE });
  const sendState = () => setTimeout(() => readym.emit("State", JSON.stringify(view())), ANSWER_MS);

  function roll(counts) {
    const dice = [];
    for (const [kind, sides] of KINDS)
      for (let n = 0; n < counts[kind]; n++) dice.push({ sides, value: scenario === "crit" ? sides : rollDie(sides) });
    return dice;
  }

  readym.onSend((name, args) => {
    if (name === "DiceReady") {
      if (scenario !== "present") sendState();
      else
        setTimeout(() => {
          const dice = roll({ d6: 0, d10: 0, d20: 5, d100: 0 });
          readym.emit("Present", "Lazy", 0, 0, 5, 0, JSON.stringify(dice.map((die) => die.value)));
        }, ANSWER_MS);
    }
    if (name === "DiceClose") setTimeout(() => readym.emit("Closed"), 0);
    if (name === "DiceSet") {
      [set] = args;
      sendState();
    }
    if (name !== "DiceThrow") return;

    const [requestId, d6, d10, d20, d100] = args;
    setTimeout(() => {
      if (scenario === "refuse") {
        readym.emit("Refused", requestId, "The dice are still rolling.");
        return;
      }
      const dice = roll({ d6, d10, d20, d100 });
      const values = dice.map((die) => die.value);
      readym.emit("Thrown", requestId, JSON.stringify(values));
      recent.unshift({ at: Date.now(), asked: "", dice, total: values.reduce((sum, value) => sum + value, 0), reason: null });
      sendState();
    }, ANSWER_MS);
  });
}
