// The window arrives as JSON: {name, gold, most, paysWages, unpaid, wage, offers: [{id, kind, name, icon, price, carried}]},
// again whenever the player's gold or goods change. A deal is answered with the Jobs mod's sentence and whether it went
// through.
export function createBridge(readym) {
  return {
    onLocale: (handler) => readym.on("Locale", handler),
    onState: (handler) => readym.on("State", (json) => handler(JSON.parse(json))),
    onAnswer: (handler) => readym.on("Answer", handler),
    onClosed: (handler) => readym.on("Closed", handler),
    signalReady: () => readym.sendForemanReady(),
    deal: (offer, count) => readym.sendForemanDeal(offer, count),
    close: () => readym.sendForemanClose(),
  };
}
