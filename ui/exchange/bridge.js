// A state arrives as JSON: {version, other, carried, max, confirmInMs, mine: {gold, confirmed}, theirs: {gold, confirmed}}.
export function createBridge(readym) {
  return {
    onLocale: (handler) => readym.on("Locale", handler),
    onState: (handler) => readym.on("State", (json) => handler(JSON.parse(json))),
    onRefused: (handler) => readym.on("Refused", handler),
    onDone: (handler) => readym.on("Done", handler),
    onClosed: (handler) => readym.on("Closed", handler),
    signalReady: () => readym.sendExchangeReady(),
    offer: (gold) => readym.sendExchangeOffer(gold),
    confirm: (version) => readym.sendExchangeConfirm(version),
    cancel: () => readym.sendExchangeCancel(),
  };
}
