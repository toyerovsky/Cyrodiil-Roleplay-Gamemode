// State: a FishingView as JSON, what to show of the line. Anchor: where the float, or where the cast would land, shows on
// the screen, as shares of its width and height, or that it is not on the screen. Charge: Space went down (true, the
// power swings from now, up in so many ms) or up. Ended: the fishing is over.
export function createBridge(readym) {
  return {
    onLocale: (handler) => readym.on("Locale", handler),
    onState: (handler) => readym.on("State", handler),
    onAnchor: (handler) => readym.on("Anchor", handler),
    onCharge: (handler) => readym.on("Charge", handler),
    onEnded: (handler) => readym.on("Ended", handler),
    signalReady: () => readym.sendFishingReady(),
  };
}
