// The party arrives as JSON (PartyView.cs): {members: [{name, leader, self, health, away}], max}, the leader first. No
// members: the player is in none. Health: percent, null while a member is not in the world.
export function createBridge(readym) {
  return {
    onLocale: (handler) => readym.on("Locale", handler),
    onState: (handler) => readym.on("State", (json) => handler(JSON.parse(json))),
    signalReady: () => readym.sendPartyReady(),
  };
}
