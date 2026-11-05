// The lore card asks the server one JSON request at a time, by a request id of its own, and gets one JSON answer under
// the same id (the PageDesk's ops: load, options, set, pick, vis, namevis, clear, roll, keep, preview, nearby, view,
// introduce, accept, decline, forget). Request id 0 is the server's own push: {op: "edit"} opens the editor,
// {op: "view", who, view} shows another player's card. The game opens the window on a push and says Closed when it shuts.
// Typing: a box of the page has the keyboard, so the game's End bind leaves the caret to it.
export function createBridge(readym) {
  return {
    onLocale: (handler) => readym.on("Locale", handler),
    onAnswer: (handler) => readym.on("Answer", (requestId, json) => handler(Number(requestId), json)),
    onClosed: (handler) => readym.on("Closed", handler),
    signalReady: () => readym.sendLoreReady(),
    ask: (requestId, json) => readym.sendLoreAsk(requestId, json),
    close: () => readym.sendLoreClose(),
    typing: (isTyping) => readym.sendLoreTyping(isTyping),
  };
}
