export function createBridge(readym) {
  return {
    onLocale: (handler) => readym.on("Locale", handler),
    onShown: (handler) => readym.on("CreatorShown", handler),
    onAnswer: (handler) => readym.on("NameAnswer", handler),
    signalReady: () => readym.sendCreatorReady(),
    submitName: (firstName, lastName) => readym.sendSubmitName(firstName, lastName),
  };
}
