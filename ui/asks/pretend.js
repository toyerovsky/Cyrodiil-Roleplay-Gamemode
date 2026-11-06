const TOTAL_MS = 30000;
const ANSWER_MS = 50;
const ANSWER_KEYS = ["y", "n"];

// Lucia Gallus invites the player to her party. #trade: Ra'jirr's trade waits under it, and the player's own invitation
// to Mazoga waits for her. #outgoing: only the player's own trade, with a long name. #introduce: a Dunmer, as a stranger
// sees them, wants to introduce themselves. #faction: an Imperial, a stranger too, invites the player to their faction. Y
// and N answer as the game would.
const SCENARIOS = {
  "": [{ id: 1, kind: "party", name: "Lucia Gallus", incoming: true, leftMs: 27000 }],
  trade: [
    { id: 1, kind: "party", name: "Lucia Gallus", incoming: true, leftMs: 27000 },
    { id: 2, kind: "trade", name: "Ra'jirr", incoming: true, leftMs: 29000 },
    { id: 3, kind: "party", name: "Mazoga gro-Malog", incoming: false, leftMs: 18000 },
  ],
  outgoing: [{ id: 3, kind: "trade", name: "Aurelianus Valerius-Maximinus", incoming: false, leftMs: 18000 }],
  introduce: [{ id: 4, kind: "introduce", name: "Dunmer", incoming: true, leftMs: 25000 }],
  faction: [{ id: 5, kind: "faction", name: "Imperial", incoming: true, leftMs: 28000 }],
};

export function pretendGame(readym) {
  let asks = SCENARIOS[location.hash.slice(1)].map((ask) => ({ ...ask, totalMs: TOTAL_MS }));

  const send = () => {
    const answerable = asks.find((ask) => ask.incoming)?.id ?? 0;
    readym.emit("State", JSON.stringify({ asks, answerable }));
  };

  readym.onSend((name) => {
    if (name === "AsksReady") setTimeout(send, 0);
  });

  document.addEventListener("keydown", (event) => {
    const answering = asks.find((ask) => ask.incoming);
    if (!answering || !ANSWER_KEYS.includes(event.key.toLowerCase())) return;
    asks = asks.filter((ask) => ask !== answering);
    setTimeout(send, ANSWER_MS);
  });
}
