// The board arrives as JSON: {board, notices: [{id, text, ageMinutes, mine}], shown, minLength, maxLength, perCharacter,
// mine, waitSeconds, expiryDays}, again whenever its notices or the reader's own change. Pinning a notice or taking one
// down is answered with a line in the player's language and whether it went through.
export function createBridge(readym) {
  return {
    onLocale: (handler) => readym.on("Locale", handler),
    onState: (handler) => readym.on("State", (json) => handler(JSON.parse(json))),
    onAnswer: (handler) => readym.on("Answer", handler),
    onClosed: (handler) => readym.on("Closed", handler),
    signalReady: () => readym.sendBoardReady(),
    post: (text) => readym.sendBoardPost(text),
    takeDown: (id) => readym.sendBoardTakeDown(id),
    close: () => readym.sendBoardClose(),
  };
}
