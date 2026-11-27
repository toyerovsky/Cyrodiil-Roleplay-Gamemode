import building from "../../locales/en/building.json";
import { fill } from "../shared/locale.js";

const ANSWER_MS = 50;

const PLOT = { id: 6, kind: "Residential", width: 12, depth: 11, widthMetres: 21.9, depthMetres: 20.1, heightMetres: 50, price: 5000, weeklyTax: 100 };

// A plot for sale and 6,200 gold. #poor: 340 gold. #yours: the reader's own plot. #taken: another's. #many: the reader
// owns as many plots as a character may. #polish: the sign in Polish.
const SCENARIOS = {
  "": { standing: "ForSale", gold: 6200, owned: 0 },
  poor: { standing: "ForSale", gold: 340, owned: 0 },
  yours: { standing: "Yours", gold: 1200, owned: 1 },
  taken: { standing: "Taken", gold: 6200, owned: 0 },
  many: { standing: "ForSale", gold: 9000, owned: 1 },
  polish: { standing: "ForSale", gold: 6200, owned: 0, locale: "pl" },
};

export function pretendGame(readym) {
  const reader = { ...(SCENARIOS[location.hash.slice(1)] ?? SCENARIOS[""]) };
  const view = () => ({ ...PLOT, standing: reader.standing, gold: reader.gold, owned: reader.owned, mostOwned: 1 });
  const send = () => setTimeout(() => readym.emit("State", JSON.stringify(view())), ANSWER_MS);
  const answer = (key, values, bought) => setTimeout(() => readym.emit("Answer", fill(building[`building.${key}`], values), bought), ANSWER_MS);

  readym.onSend((message) => {
    if (message === "PlotReady") {
      if (reader.locale) readym.emit("Locale", reader.locale);
      send();
    }
    if (message === "PlotClose") setTimeout(() => readym.emit("Closed"), 0);
    if (message !== "PlotBuy") return;

    reader.gold -= PLOT.price;
    reader.standing = "Yours";
    reader.owned += 1;
    answer("sale_bought", { id: PLOT.id, price: PLOT.price, tax: PLOT.weeklyTax }, true);
    send();
  });
}
