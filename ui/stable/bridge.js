// The window arrives as JSON: {horses: [{id, price, owned}], gold}, the game's horses with their prices, the one the
// character owns marked, and the gold carried, again whenever the horse owned or the gold changes. A purchase is answered
// with a line in the player's language and whether it went through.
export function createBridge(readym) {
  return {
    onLocale: (handler) => readym.on("Locale", handler),
    onState: (handler) => readym.on("State", (json) => handler(JSON.parse(json))),
    onAnswer: (handler) => readym.on("Answer", handler),
    onClosed: (handler) => readym.on("Closed", handler),
    signalReady: () => readym.sendStableReady(),
    buy: (horse) => readym.sendStableBuy(horse),
    close: () => readym.sendStableClose(),
  };
}
