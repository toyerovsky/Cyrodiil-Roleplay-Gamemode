// The badge arrives as JSON (SanctuaryHudView.cs): {inside, aggressor, secondsLeft}, at every change of the player's place
// in the Sanctuary or streak. The seconds are as they were when it was sent, the page counts them down.
export function createBridge(readym) {
  return {
    onLocale: (handler) => readym.on("Locale", handler),
    onState: (handler) => readym.on("State", (json) => handler(JSON.parse(json))),
    signalReady: () => readym.sendSanctuaryReady(),
  };
}
