// A guide's card arrives as JSON: {topic, mode, title, purpose, steps: [{text, keys}], facts: [{label, value}]}. In mode
// Use, Continue lets what waited behind the card go on and Esc only closes it. The welcome (Entry) and a guide read again
// from the menu (Read) only close. Closed: the card is gone, whoever closed it.
export function createBridge(readym) {
  return {
    onLocale: (handler) => readym.on("Locale", handler),
    onState: (handler) => readym.on("State", (json) => handler(JSON.parse(json))),
    onClosed: (handler) => readym.on("Closed", handler),
    signalReady: () => readym.sendGuideReady(),
    goOn: () => readym.sendGuideContinue(),
    close: () => readym.sendGuideClose(),
  };
}
