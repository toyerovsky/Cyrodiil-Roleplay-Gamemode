// The window arrives as JSON: {id, kind, width, depth, widthMetres, depthMetres, heightMetres, price, weeklyTax, standing
// (ForSale, Yours or Taken), gold, owned, mostOwned}: the plot at the sign, whose it is, and the reader's gold and plots,
// again whenever they change. A purchase is answered with a line in the player's language and whether it went through.
export function createBridge(readym) {
  return {
    onLocale: (handler) => readym.on("Locale", handler),
    onState: (handler) => readym.on("State", (json) => handler(JSON.parse(json))),
    onAnswer: (handler) => readym.on("Answer", handler),
    onClosed: (handler) => readym.on("Closed", handler),
    signalReady: () => readym.sendPlotReady(),
    buy: () => readym.sendPlotBuy(),
    close: () => readym.sendPlotClose(),
  };
}
