// The asks arrive as JSON (AsksView.cs): {asks: [{id, kind, name, incoming, leftMs, totalMs}], answerable}, the oldest
// first. The game answers the one marked answerable with Y and N: the page only shows them.
export function createBridge(readym) {
  return {
    onLocale: (handler) => readym.on("Locale", handler),
    onState: (handler) => readym.on("State", (json) => handler(JSON.parse(json))),
    signalReady: () => readym.sendAsksReady(),
  };
}
