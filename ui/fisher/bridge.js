// The window arrives as a FisherView in JSON: {gold, most, catch, shop, repairs}, again whenever the purse or the pack
// changes. Every deal is answered with a line in the player's language and whether it went through. The records board is
// asked for as its tab opens and comes once, as a FisherRecordsView in JSON: {species: [{species, name, top, own}]}, each
// fish of top and own a {rank, name, lengthCm, grams, at, mine}. A board that could not be read is answered as a deal
// that did not go through.
export function createBridge(readym) {
  return {
    onLocale: (handler) => readym.on("Locale", handler),
    onState: (handler) => readym.on("State", (json) => handler(JSON.parse(json))),
    onAnswer: (handler) => readym.on("Answer", handler),
    onRecords: (handler) => readym.on("Records", (json) => handler(JSON.parse(json))),
    onClosed: (handler) => readym.on("Closed", handler),
    signalReady: () => readym.sendFisherReady(),
    buy: (ware, count) => readym.sendFisherBuy(ware, count),
    sell: (item, count) => readym.sendFisherSell(item, count),
    sellAll: () => readym.sendFisherSellAll(),
    repair: (item) => readym.sendFisherRepair(item),
    askRecords: () => readym.sendFisherAskRecords(),
    close: () => readym.sendFisherClose(),
  };
}
