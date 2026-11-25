// FishingReach.Nearest: a cast at no power lands this far, in centimetres. tests/fishing.test.js reads it from the C#.
export const NEAREST_CAST_CM = 150;

// The cast's power while Space is held, as FishingCharge.PowerAt: up from nothing to full in upMs, back down, up again.
export function findPower(heldMs, upMs) {
  const phase = (Math.max(0, heldMs) / upMs) % 2;
  return phase <= 1 ? phase : 2 - phase;
}
