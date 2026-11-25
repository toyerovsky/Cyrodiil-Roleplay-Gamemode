// State: a FishingView as JSON, what to show of the line. Anchor: where the float, or where the cast would land, shows on
// the screen, as shares of its width and height, or that it is not on the screen. Ended: the fishing is over.
export function createBridge(readym) {
  return {
    onLocale: (handler) => readym.on("Locale", handler),
    onState: (handler) => readym.on("State", handler),
    onAnchor: (handler) => readym.on("Anchor", handler),
    onEnded: (handler) => readym.on("Ended", handler),
    signalReady: () => readym.sendFishingReady(),
  };
}
