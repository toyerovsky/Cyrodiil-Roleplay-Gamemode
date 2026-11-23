// A key or the mouse in a page says the player is at the keyboard, though a window holds the character still and the
// server sees no step. Every page reports it (Away/PresenceReports.cs), at most every PRESENCE_EVERY_MS, and the game passes
// it on to the server more rarely still.
export const PRESENCE_EVERY_MS = 5000;
const SIGNS = ["keydown", "pointerdown", "pointermove", "wheel"];

let noted = () => {};

// A sign no event of the page's own carries (the game's cursor moving in one of its menus), under the same pacing.
export function notePresence() {
  noted();
}

export function watchPresence(readym, now = () => Date.now()) {
  let reportedAt = -Infinity;
  const report = () => {
    const at = now();
    if (at - reportedAt < PRESENCE_EVERY_MS) return;
    reportedAt = at;
    readym.sendPresence();
  };
  noted = report;
  for (const sign of SIGNS) window.addEventListener(sign, report, { capture: true, passive: true });
}
