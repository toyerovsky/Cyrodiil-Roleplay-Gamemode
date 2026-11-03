const MAX_MEMBERS = 6;

// A party of three led by the player, Lucia wounded. #member: Lucia leads and the player is one of six, with a long
// name among them, one far away, one down and one not in the world.
const PARTIES = {
  "": [
    { name: "Marius Varro", leader: true, self: true, health: 100, away: false },
    { name: "Lucia Gallus", leader: false, self: false, health: 45, away: false },
    { name: "Ra'jirr", leader: false, self: false, health: 85, away: false },
  ],
  member: [
    { name: "Lucia Gallus", leader: true, self: false, health: 70, away: false },
    { name: "Marius Varro", leader: false, self: true, health: 100, away: false },
    { name: "Ra'jirr", leader: false, self: false, health: 0, away: false },
    { name: "Mazoga gro-Malog", leader: false, self: false, health: 95, away: true },
    { name: "Aurelianus Valerius-Maximinus", leader: false, self: false, health: 20, away: false },
    { name: "Hjalti Early-Beard", leader: false, self: false, health: null, away: true },
  ],
};

export function pretendGame(readym) {
  const members = PARTIES[location.hash.slice(1)];
  readym.onSend((name) => {
    if (name === "PartyReady") setTimeout(() => readym.emit("State", JSON.stringify({ members, max: MAX_MEMBERS })), 0);
  });
}
