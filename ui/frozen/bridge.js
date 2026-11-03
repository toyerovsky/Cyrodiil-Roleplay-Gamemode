// The notice says why, in the player's language, while the staff hold the character. Thawed ends it.
export function createBridge(readym) {
  return {
    onLocale: (handler) => readym.on("Locale", handler),
    onNotice: (handler) => readym.on("Notice", handler),
    onThawed: (handler) => readym.on("Thawed", handler),
    signalReady: () => readym.sendFrozenReady(),
  };
}
