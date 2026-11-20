export function createBridge(readym) {
  return {
    onLocale: (handler) => readym.on("Locale", handler),
    onState: (handler) => readym.on("State", (json) => handler(JSON.parse(json))),
    onRefused: (handler) => readym.on("Refused", handler),
    onClosed: (handler) => readym.on("Closed", handler),
    signalReady: () => readym.sendGamblingReady(),
    close: () => readym.sendGamblingClose(),
    invite: (opponent, stake, mode) => readym.sendGamblingInvite(opponent, stake, mode),
    act: (game, version, action, amount) => readym.sendGamblingAct(game, version, action, amount),
    refresh: () => readym.sendGamblingRefresh(),
  };
}
