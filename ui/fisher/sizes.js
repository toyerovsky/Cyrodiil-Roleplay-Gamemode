import { fill } from "../shared/locale.js";

const GRAMS_IN_KILOGRAM = 1000;

// A fish's size as the records board writes it, in the words of the page's texts (t) and the reader's numbers.
export const formatLength = (cm, t, locale) => fill(t["records.length"], { length: cm.toLocaleString(locale) });

// Grams under a kilogram, then kilograms to one decimal, as the catch's card of the fishing page shows a weight.
export function formatWeight(grams, t, locale) {
  if (grams < GRAMS_IN_KILOGRAM) return fill(t["records.weight_grams"], { grams: grams.toLocaleString(locale) });
  const kilograms = (grams / GRAMS_IN_KILOGRAM).toLocaleString(locale, { maximumFractionDigits: 1 });
  return fill(t["records.weight_kilograms"], { kilograms });
}

export const formatDay = (milliseconds, locale) => new Intl.DateTimeFormat(locale, { dateStyle: "medium" }).format(milliseconds);
