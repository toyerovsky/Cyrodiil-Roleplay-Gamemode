// Lines arrive as JSON: [{kind, text, at}].
export function createBridge(readym) {
  return {
    onLocale: (handler) => readym.on("Locale", handler),
    onLines: (handler) => readym.on("Line", (json) => handler(JSON.parse(json))),
    onHistory: (handler) => readym.on("History", (json) => handler(JSON.parse(json))),
    onOpen: (handler) => readym.on("Open", handler),
    onDraft: (handler) => readym.on("Draft", handler),
    requestHistory: () => readym.sendRequestHistory(),
    submit: (text) => readym.sendSubmit(text),
    close: () => readym.sendClose(),
    reportBlur: () => readym.sendBlurred(),
  };
}
