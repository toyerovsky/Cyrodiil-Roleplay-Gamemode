import trader from "../../locales/en/trader.json";
import { fill } from "../shared/locale.js";
import { WARE_ICONS } from "./wares.js";

const GOLD = 120;
const POOR = 6;
const MOST_PIECES = 20;
const ANSWER_MS = 50;

// The game's values of a few wares of every kind, priced as the server prices them: 140 %, rounded up. The torch, which the
// game values at nothing, has its own price in stocks.json.
const VALUES = {
  Breadloaf: 1, Apple: 1, CheeseWheel: 1, Ham: 1, Sweetroll: 1, DrinkAle: 5, DrinkMead: 2, DrinkWine2TamikaGood: 8,
  LowerShirt04: 1, LowerPants04: 1, MiddleShirt02: 2, PotionRestoreHealthW: 53, PotionCureDisease: 140, Lockpick: 3,
  RepairHammer: 10, MortarPestle: 25, WeapIronDagger: 6, WeapIronLongsword: 20, WeapIronBow: 11, Arrow1Iron: 1, FurCuirass: 15,
  LeatherBoots: 10, IronShield: 45, Book1CheapGuideImperialCity: 1, Book1CheapBriefHistoryoftheEmpirev1: 1,
};
const PRICES = { Torch02: 3 };
const CATEGORIES = {
  Breadloaf: "food", Apple: "food", CheeseWheel: "food", Ham: "food", Sweetroll: "food", DrinkAle: "drink", DrinkMead: "drink",
  DrinkWine2TamikaGood: "drink", LowerShirt04: "clothing", LowerPants04: "clothing", MiddleShirt02: "clothing",
  PotionRestoreHealthW: "potions", PotionCureDisease: "potions", Torch02: "tools", Lockpick: "tools", RepairHammer: "tools",
  MortarPestle: "tools", WeapIronDagger: "weapons", WeapIronLongsword: "weapons", WeapIronBow: "weapons", Arrow1Iron: "weapons",
  FurCuirass: "armor", LeatherBoots: "armor", IronShield: "armor", Book1CheapGuideImperialCity: "books",
  Book1CheapBriefHistoryoftheEmpirev1: "books",
};
const markUp = (value) => Math.floor((value * 140 + 99) / 100);

// The general store with 120 gold. #poor: 6 gold, so most wares are too dear.
export function pretendGame(readym) {
  let gold = location.hash.slice(1) === "poor" ? POOR : GOLD;
  const wares = Object.keys(CATEGORIES)
    .filter((id) => id in WARE_ICONS)
    .map((id) => ({ id, category: CATEGORIES[id], price: PRICES[id] ?? markUp(VALUES[id]), weight: 1.5 }));
  const send = () => setTimeout(() => readym.emit("State", JSON.stringify({ shop: "general", gold, most: MOST_PIECES, wares })), ANSWER_MS);

  readym.onSend((name, args) => {
    if (name === "TraderReady") send();
    if (name === "TraderClose") setTimeout(() => readym.emit("Closed"), 0);
    if (name !== "TraderBuy") return;

    const [id, count] = args;
    const total = wares.find((ware) => ware.id === id).price * count;
    if (total > gold) {
      setTimeout(() => readym.emit("Answer", fill(trader["trader.too_poor"], { price: total, gold }), false), ANSWER_MS);
      return;
    }

    gold -= total;
    setTimeout(() => readym.emit("Answer", fill(trader["trader.bought"][total === 1 ? "one" : "other"], { count: total }), true), ANSWER_MS);
    send();
  });
}
