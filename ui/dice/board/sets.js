// The twelve dice sets, in the order the models were made. A set is only a look: anyone may throw any. Their names are
// in the page's texts (set.<id>), and DiceSets.cs on the server holds the same ids.
export const SETS = ["altmer", "argonian", "ayleid", "bosmer", "breton", "daedric", "dunmer", "imperial", "khajiit", "nord", "orc", "redguard"].map(
  (id) => ({ id }),
);

export const DEFAULT_SET = "imperial";

export const isSet = (id) => SETS.some((set) => set.id === id);
