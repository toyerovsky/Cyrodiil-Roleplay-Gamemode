// The menu arrives as JSON: {tab, character, account, help, languages, language}, again after a language is picked. The
// page closes on Closed, whoever closed it.
export function createBridge(readym) {
  return {
    onLocale: (handler) => readym.on("Locale", handler),
    onState: (handler) => readym.on("State", (json) => handler(JSON.parse(json))),
    onClosed: (handler) => readym.on("Closed", handler),
    signalReady: () => readym.sendMenuReady(),
    close: () => readym.sendMenuClose(),
    setLanguage: (locale) => readym.sendMenuLanguage(locale),
  };
}
