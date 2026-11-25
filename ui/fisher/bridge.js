// The window arrives as a FisherView in JSON: {gold, most, catch, shop, repairs}, again whenever the purse or the pack
// changes. Every deal is answered with a line in the player's language and whether it went through.
export function createBridge(readym) {
  return {
    onLocale: (handler) => readym.on("Locale", handler),
    onState: (handler) => readym.on("State", (json) => handler(JSON.parse(json))),
    onAnswer: (handler) => readym.on("Answer", handler),
    onClosed: (handler) => readym.on("Closed", handler),
    signalReady: () => readym.sendFisherReady(),
    buy: (ware, count) => readym.sendFisherBuy(ware, count),
    sell: (item, count) => readym.sendFisherSell(item, count),
    sellAll: () => readym.sendFisherSellAll(),
    repair: (item) => readym.sendFisherRepair(item),
    close: () => readym.sendFisherClose(),
  };
}
