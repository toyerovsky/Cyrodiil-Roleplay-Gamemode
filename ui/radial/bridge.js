// A wheel arrives as JSON: {title, items: [{id, label, icon, disabled, reason}]}, with the key that opened it.
export function createBridge(readym) {
  return {
    onLocale: (handler) => readym.on("Locale", handler),
    onWheel: (handler) => readym.on("Wheel", (json, key) => handler(JSON.parse(json), key)),
    onHidden: (handler) => readym.on("Hidden", handler),
    signalReady: () => readym.sendWheelReady(),
    pick: (id) => readym.sendWheelPick(id),
    close: () => readym.sendWheelClose(),
  };
}
