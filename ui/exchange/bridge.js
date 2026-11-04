// A state arrives as JSON: {version, other, carried, max, confirmInMs, items, mine, theirs}. An offer is
// {gold, items, confirmed}, an item {key, name, used, count}, and `items` on its own is what the player can offer.
export function createBridge(readym) {
  return {
    onLocale: (handler) => readym.on("Locale", handler),
    onState: (handler) => readym.on("State", (json) => handler(JSON.parse(json))),
    onRefused: (handler) => readym.on("Refused", handler),
    onDone: (handler) => readym.on("Done", handler),
    onClosed: (handler) => readym.on("Closed", handler),
    signalReady: () => readym.sendExchangeReady(),
    offer: (gold) => readym.sendExchangeOffer(gold),
    offerItem: (key, count) => readym.sendExchangeOfferItem(key, count),
    confirm: (version) => readym.sendExchangeConfirm(version),
    cancel: () => readym.sendExchangeCancel(),
  };
}
