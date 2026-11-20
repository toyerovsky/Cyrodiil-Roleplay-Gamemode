// Warning: the seconds left before [AFK] shows over the player's head. Marked: the seconds since the player last did
// anything. The page counts on from either by itself. Ended: the player is back.
export function createBridge(readym) {
  return {
    onLocale: (handler) => readym.on("Locale", handler),
    onWarning: (handler) => readym.on("Warning", handler),
    onMarked: (handler) => readym.on("Marked", handler),
    onEnded: (handler) => readym.on("Ended", handler),
    signalReady: () => readym.sendAwayReady(),
  };
}
