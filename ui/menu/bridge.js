// The menu arrives as JSON: {tab, character, account, help, dice, lore, languages, language}, again after a language, a
// dice set or the name's visibility is picked. The page closes on Closed, whoever closed it. Its dice tab opens the dice
// board and its lore tab the lore card, each closing the menu.
export function createBridge(readym) {
  return {
    onLocale: (handler) => readym.on("Locale", handler),
    onState: (handler) => readym.on("State", (json) => handler(JSON.parse(json))),
    onClosed: (handler) => readym.on("Closed", handler),
    signalReady: () => readym.sendMenuReady(),
    close: () => readym.sendMenuClose(),
    setLanguage: (locale) => readym.sendMenuLanguage(locale),
    openDice: () => readym.sendMenuDice(),
    chooseDiceSet: (set) => readym.sendMenuDiceSet(set),
    openLore: () => readym.sendMenuLore(),
    setNamePublic: (isPublic) => readym.sendMenuLoreName(isPublic),
  };
}
