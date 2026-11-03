// The welcome arrives as JSON: {nickname, firstVisit, characters: [{firstName, lastName, lastPlayedAt, playedSeconds}],
// emptySlots, lockedSlots}. Closed: the player chose and the game has taken over.
export function createBridge(readym) {
  return {
    onLocale: (handler) => readym.on("Locale", handler),
    onWelcome: (handler) => readym.on("Welcome", (json) => handler(JSON.parse(json))),
    onClosed: (handler) => readym.on("Closed", handler),
    signalReady: () => readym.sendWelcomeReady(),
    play: () => readym.sendWelcomePlay(),
    createCharacter: () => readym.sendWelcomeNew(),
  };
}
