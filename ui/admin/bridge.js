// The panel arrives as JSON (AdminView.cs): {tab, focus, acting, onDuty, tabs, allowed, players, places, weathers,
// traders}, again whenever the players or the duty change. What it asks for comes back by name (AdminParts.cs): a
// player's card, the sanctions, a page of the audit, the items found. A command it ran is answered in the staff member's
// language, done or refused.
export function createBridge(readym) {
  return {
    onLocale: (handler) => readym.on("Locale", handler),
    onState: (handler) => readym.on("State", (json) => handler(JSON.parse(json))),
    onPart: (handler) => readym.on("Part", (kind, json) => handler(kind, JSON.parse(json))),
    onAnswer: (handler) => readym.on("Answer", handler),
    onClosed: (handler) => readym.on("Closed", handler),
    signalReady: () => readym.sendAdminReady(),
    run: (line) => readym.sendAdminRun(line),
    ask: (kind, argument) => readym.sendAdminAsk(kind, argument),
    close: () => readym.sendAdminClose(),
  };
}
