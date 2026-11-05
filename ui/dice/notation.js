import { fill } from "../shared/locale.js";

// A roll as its page writes it, from its dice ({sides, value}) in the order the server rolled them: d6s, d10s, d20s,
// d100s. The dice's names come from the dice page's texts (ui.dice.die.d20), so a Polish roll reads "2k6".
export const SIDES = [6, 10, 20, 100];

export const countDice = (dice) => Object.fromEntries(SIDES.map((sides) => [sides, dice.filter((die) => die.sides === sides).length]));

export const sumDice = (dice) => dice.reduce((sum, die) => sum + die.value, 0);

export function formatAsked(dice, t) {
  const counts = countDice(dice);
  return SIDES.filter((sides) => counts[sides] > 0)
    .map((sides) => `${counts[sides]}${t[`die.d${sides}`]}`)
    .join(" + ");
}

export const formatDice = (dice, t) => dice.map((die) => fill(t.die_value, { die: t[`die.d${die.sides}`], value: die.value })).join(", ");

// The board's counts ({d6, d10, d20, d100}) and the server's values as dice.
export function toDice(counts, values) {
  const dice = [];
  let at = 0;
  for (const sides of SIDES) for (let n = 0; n < (counts[`d${sides}`] ?? 0); n++) dice.push({ sides, value: values[at++] });
  return dice;
}
