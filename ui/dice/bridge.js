// The board arrives as JSON (DiceView.cs): {set, sets, recent, maxDice}, again after a roll or a new set. A throw is the
// page's own request id and its dice counted d6, d10, d20, d100, answered by Thrown (the values in that order) or
// Refused. ThrowOnBoard: /roll with the board open. Present: a roll the server made for something else, shown on a small
// board that takes no input, after which the page says it is done.
const readValues = (values) => (typeof values === "string" ? JSON.parse(values) : Array.from(values));

export function createBridge(readym) {
  return {
    onLocale: (handler) => readym.on("Locale", handler),
    onState: (handler) => readym.on("State", (json) => handler(JSON.parse(json))),
    onThrown: (handler) => readym.on("Thrown", (requestId, values) => handler(Number(requestId), readValues(values))),
    onRefused: (handler) => readym.on("Refused", (requestId, text) => handler(Number(requestId), text)),
    onThrowOnBoard: (handler) => readym.on("ThrowOnBoard", (d6, d10, d20, d100) => handler({ d6, d10, d20, d100 })),
    onPresent: (handler) =>
      readym.on("Present", (reason, d6, d10, d20, d100, values) => handler(reason, { d6, d10, d20, d100 }, readValues(values))),
    onClosed: (handler) => readym.on("Closed", handler),
    signalReady: () => readym.sendDiceReady(),
    close: () => readym.sendDiceClose(),
    throwDice: (requestId, counts) => readym.sendDiceThrow(requestId, counts.d6, counts.d10, counts.d20, counts.d100),
    chooseSet: (set) => readym.sendDiceSet(set),
    presented: () => readym.sendDicePresented(),
  };
}
