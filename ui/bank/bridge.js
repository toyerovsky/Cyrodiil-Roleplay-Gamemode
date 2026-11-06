// The window arrives as JSON: {balance, gold}, the gold at the bank and the gold carried, again whenever either changes.
// A deposit or a withdrawal is answered with a line in the player's language and whether it went through.
export function createBridge(readym) {
  return {
    onLocale: (handler) => readym.on("Locale", handler),
    onState: (handler) => readym.on("State", (json) => handler(JSON.parse(json))),
    onAnswer: (handler) => readym.on("Answer", handler),
    onClosed: (handler) => readym.on("Closed", handler),
    signalReady: () => readym.sendBankReady(),
    deposit: (amount) => readym.sendBankDeposit(amount),
    withdraw: (amount) => readym.sendBankWithdraw(amount),
    close: () => readym.sendBankClose(),
  };
}
