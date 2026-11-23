// The game hands every page its mouse cursor each tick while it draws one, by calling window.__readym_cursor with the
// cursor's place on the page, and calls it once with (-1, -1) when the cursor goes. The game draws a cursor exactly
// while one of its own menus is up, and tells a mod nothing else about them. The platform puts a function of its own
// under that name, before or after a page's script runs, to answer whether the cursor is over something of the page.
// So the name is taken over: whatever is put there is kept and still called, and the page hears when the cursor comes
// and goes, and when it moves while shown (the player at work in one of the game's menus).
const GONE = -1;

export function watchGameCursor(report, moved, target = window) {
  let platform = target.__readym_cursor;
  let shown = false;
  let lastX = GONE;
  let lastY = GONE;

  function relay(x, y) {
    const now = !(x === GONE && y === GONE);
    if (now !== shown) {
      shown = now;
      report(now);
    } else if (now && (x !== lastX || y !== lastY)) {
      moved();
    }
    lastX = x;
    lastY = y;
    return typeof platform === "function" ? platform.call(this, x, y) : undefined;
  }

  Object.defineProperty(target, "__readym_cursor", {
    configurable: true,
    get: () => relay,
    set: (handler) => {
      platform = handler;
    },
  });
}
