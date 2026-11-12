import stable from "../../locales/en/stable.json";
import { fill } from "../shared/locale.js";

const ANSWER_MS = 50;

// The server's default prices, what the game's own stables ask.
const PRICES = { Paint: 500, Bay: 1000, Chestnut: 2500, White: 4000, Black: 5000 };
const NAMES = { Paint: "paint", Bay: "bay", Chestnut: "chestnut", White: "white", Black: "black" };

// A character without a horse and 1,340 gold. #owned: a Bay horse already and 6,200 gold, so a purchase replaces it.
// #poor: 40 gold, too little for any horse.
const SCENARIOS = {
  "": { owned: null, gold: 1340 },
  owned: { owned: "Bay", gold: 6200 },
  poor: { owned: null, gold: 40 },
};

export function pretendGame(readym) {
  const character = { ...(SCENARIOS[location.hash.slice(1)] ?? SCENARIOS[""]) };
  const view = () => ({
    horses: Object.entries(PRICES).map(([id, price]) => ({ id, price, owned: id === character.owned })),
    gold: character.gold,
  });
  const send = () => setTimeout(() => readym.emit("State", JSON.stringify(view())), ANSWER_MS);
  const name = (id) => stable[`stable.horse.${NAMES[id]}`];
  const answer = (key, values, bought) => setTimeout(() => readym.emit("Answer", fill(stable[`stable.${key}`], values), bought), ANSWER_MS);

  readym.onSend((message, args) => {
    if (message === "StableReady") send();
    if (message === "StableClose") setTimeout(() => readym.emit("Closed"), 0);
    if (message !== "StableBuy") return;

    const [id] = args;
    const price = PRICES[id];
    if (price > character.gold) {
      answer("too_poor", { horse: name(id), price, gold: character.gold }, false);
      return;
    }

    const before = character.owned;
    character.gold -= price;
    character.owned = id;
    if (before) answer("bought_instead", { horse: name(id), price, old: name(before) }, true);
    else answer("bought", { horse: name(id), price }, true);
    send();
  });
}
