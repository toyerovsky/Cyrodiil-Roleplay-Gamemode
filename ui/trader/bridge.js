// The window arrives as JSON: {shop, gold, most, wares: [{id, category, price, weight}]}, again whenever the player's
// gold changes. A buy is answered with a line in the player's language and whether it went through.
export function createBridge(readym) {
  return {
    onLocale: (handler) => readym.on("Locale", handler),
    onState: (handler) => readym.on("State", (json) => handler(JSON.parse(json))),
    onAnswer: (handler) => readym.on("Answer", handler),
    onClosed: (handler) => readym.on("Closed", handler),
    signalReady: () => readym.sendTraderReady(),
    buy: (ware, count) => readym.sendTraderBuy(ware, count),
    close: () => readym.sendTraderClose(),
  };
}
