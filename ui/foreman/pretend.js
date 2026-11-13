const ANSWER_MS = 50;
const MOST_PIECES = 20;

// The three foremen as the server shows them. "": the lumber camp leader, with 60 gold and 12 firewood. #quarry: the
// quarry foreman with 20 gold, too little for the warhammer. #docks: the dock foreman, owing 7 crates.
const FOREMEN = {
  "": {
    name: "Lumber Camp Leader",
    gold: 60,
    paysWages: false,
    unpaid: 0,
    offers: [
      { id: "buy:0", kind: "buy", name: "Lumber Axe", icon: "WeapIronWarAxe", price: 50, carried: 0 },
      { id: "sell:Firewood", kind: "sell", name: "Firewood", icon: "", price: 1, carried: 12 },
    ],
  },
  quarry: {
    name: "Quarry Foreman",
    gold: 20,
    paysWages: false,
    unpaid: 0,
    offers: [
      { id: "buy:0", kind: "buy", name: "Iron Warhammer", icon: "WeapIronMace", price: 150, carried: 0 },
      { id: "sell:Stone Brick", kind: "sell", name: "Stone Brick", icon: "", price: 1, carried: 5 },
    ],
  },
  docks: { name: "Dock Foreman", gold: 3, paysWages: true, unpaid: 7, offers: [] },
};

export function pretendGame(readym) {
  const foreman = structuredClone(FOREMEN[location.hash.slice(1)] ?? FOREMEN[""]);
  const send = () =>
    setTimeout(() => readym.emit("State", JSON.stringify({ ...foreman, most: MOST_PIECES, wage: 1 })), ANSWER_MS);
  const answer = (text, done) => setTimeout(() => readym.emit("Answer", text, done), ANSWER_MS);

  readym.onSend((name, args) => {
    if (name === "ForemanReady") send();
    if (name === "ForemanClose") setTimeout(() => readym.emit("Closed"), 0);
    if (name !== "ForemanDeal") return;

    const [id, count] = args;
    if (id === "pay") {
      const gold = foreman.unpaid;
      foreman.gold += gold;
      foreman.unpaid = 0;
      answer(`The foreman pays you ${gold} gold for ${gold} crates.`, true);
      send();
      return;
    }

    const offer = foreman.offers.find((candidate) => candidate.id === id);
    if (offer.kind === "buy") {
      const price = offer.price * count;
      if (price > foreman.gold) {
        answer(`That is ${price} gold, and you carry ${foreman.gold}.`, false);
        return;
      }
      foreman.gold -= price;
      answer(`You buy ${count} ${offer.name} for ${price} gold.`, true);
    } else {
      const sold = Math.min(count, offer.carried);
      offer.carried -= sold;
      foreman.gold += sold * offer.price;
      answer(`You sell ${sold} ${offer.name} for ${sold * offer.price} gold.`, true);
    }
    send();
  });
}
