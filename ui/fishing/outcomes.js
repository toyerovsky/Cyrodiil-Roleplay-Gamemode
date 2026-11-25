// Each way a bite ends and the key of its line in ui.fishing.
export const OUTCOME_TEXTS = Object.freeze({
  Caught: "caught",
  Found: "found",
  LostEarly: "lost_early",
  LostNibble: "lost_nibble",
  LostLate: "lost_late",
  LostSnapped: "lost_snapped",
  LostSlack: "lost_slack",
  LostRunaway: "lost_runaway",
});

export const isCatch = (kind) => kind === "Caught" || kind === "Found";
