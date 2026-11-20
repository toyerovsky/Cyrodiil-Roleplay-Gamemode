import auction from "../../locales/en/auction.json";
import { fill } from "../shared/locale.js";
import { findFee } from "./lots.js";

const ANSWER_MS = 50;
const PAGE_SIZE = 20;
const STEP_PERCENT = 5;
const CUT_PERCENT = 5;
const HOUR_MS = 3_600_000;
const MINUTE_MS = 60_000;
const TERMS = { feePercent: 2, cutPercent: CUT_PERCENT, hours: [12, 24, 48], maxLots: 10, maxPrice: 100000 };

const LOTS = [
  { name: "Iron Dagger", count: 1, used: false, category: "Weapons", startPrice: 20, buyoutPrice: 60, topBid: null, bids: 0, endsInMs: 5 * HOUR_MS + 12 * MINUTE_MS },
  { name: "Elven Longsword", count: 1, used: true, category: "Weapons", startPrice: 400, buyoutPrice: 1200, topBid: 520, bids: 3, endsInMs: 40 * MINUTE_MS },
  { name: "Steel Arrow", count: 40, used: false, category: "Weapons", startPrice: null, buyoutPrice: 90, topBid: null, bids: 0, endsInMs: 22 * HOUR_MS },
  { name: "Glass Cuirass", count: 1, used: false, category: "Armour", startPrice: 2500, buyoutPrice: null, topBid: 2600, bids: 1, endsInMs: 3 * MINUTE_MS, leading: true },
  { name: "Fine Shirt", count: 2, used: false, category: "Clothing", startPrice: 15, buyoutPrice: null, topBid: null, bids: 0, endsInMs: 11 * HOUR_MS, mine: true },
  { name: "Potion of Light Healing", count: 5, used: false, category: "Potions", startPrice: 30, buyoutPrice: 75, topBid: null, bids: 0, endsInMs: 47 * HOUR_MS },
  { name: "Nirnroot", count: 3, used: false, category: "Ingredients", startPrice: 60, buyoutPrice: 150, topBid: 70, bids: 2, endsInMs: 9 * HOUR_MS },
  { name: "A Dance in Fire, v 1", count: 1, used: false, category: "Books", startPrice: null, buyoutPrice: 25, topBid: null, bids: 0, endsInMs: 30 * HOUR_MS },
  { name: "Novice Mortar & Pestle", count: 1, used: true, category: "Other", startPrice: 10, buyoutPrice: null, topBid: null, bids: 0, endsInMs: 18 * HOUR_MS },
];

const ITEMS = [
  { key: "00019171:BF800000:BF800000", name: "Iron Dagger", used: false, count: 2, category: "Weapons" },
  { key: "000229B9:42200000:BF800000", name: "Steel Dagger", used: true, count: 1, category: "Weapons" },
  { key: "0000C3A1:BF800000:BF800000", name: "Iron Arrow", used: false, count: 60, category: "Weapons" },
  { key: "00098304:BF800000:BF800000", name: "Potion of Healing", used: false, count: 4, category: "Potions" },
  { key: "00033690:BF800000:BF800000", name: "Bonemeal", used: false, count: 7, category: "Ingredients" },
];

const PARCELS = [
  { reason: "Sale", gold: 142, name: "Fur Boots", count: 1, used: false },
  { reason: "Won", gold: 0, name: "Silver Longsword", count: 1, used: false },
  { reason: "Outbid", gold: 85, name: "Elven Bow", count: 1, used: false },
];

// The house as each scenario opens it. #empty: nothing anywhere. #poor: 5 gold to pay with. #full: as many lots up as
// the house allows. #many: more lots than a page shows. #long: names and sums long enough to test the width.
function buildScenario(name) {
  const lots = LOTS.map((lot) => ({ mine: false, leading: false, ...lot }));
  const base = { gold: 640, lots, items: ITEMS.map((item) => ({ ...item })), parcels: PARCELS.map((parcel) => ({ ...parcel })) };
  if (name === "empty") return { gold: 0, lots: [], items: [], parcels: [] };
  if (name === "poor") return { ...base, gold: 5 };
  if (name === "full")
    return { ...base, lots: [...lots, ...Array.from({ length: TERMS.maxLots - 1 }, (_, at) => ({ ...LOTS[0], name: `Iron Mace ${at + 1}`, mine: true }))] };
  if (name === "many")
    return { ...base, lots: Array.from({ length: PAGE_SIZE + 6 }, (_, at) => ({ ...LOTS[at % LOTS.length], mine: false, leading: false, endsInMs: (at + 1) * HOUR_MS })) };
  if (name === "long")
    return { ...base, gold: 2_147_483_000, lots: [{ ...LOTS[1], name: "Elven Longsword of the Ancient Silvermoon Order", buyoutPrice: 99999, topBid: 99000, mine: false }, ...lots] };
  return base;
}

const findMinimum = (lot) => {
  if (lot.startPrice === null) return 0;
  return lot.topBid === null ? lot.startPrice : lot.topBid + Math.max(1, Math.floor((lot.topBid * STEP_PERCENT) / 100));
};

export function pretendGame(readym) {
  const house = buildScenario(location.hash.slice(1));
  const opened = Date.now();
  let nextId = 1;
  house.lots.forEach((lot) => { lot.id = nextId++; lot.endsAt = opened + lot.endsInMs; });
  house.parcels.forEach((parcel) => { parcel.id = nextId++; });
  const query = { text: "", category: "All", page: 0 };

  const showLot = (lot) => ({
    id: lot.id, name: lot.name, count: lot.count, used: lot.used, category: lot.category, startPrice: lot.startPrice, buyoutPrice: lot.buyoutPrice,
    topBid: lot.topBid, bids: lot.bids, minimumBid: findMinimum(lot), endsInMs: Math.max(0, lot.endsAt - Date.now()), mine: lot.mine, leading: lot.leading,
  });

  function buildView() {
    const found = house.lots
      .filter((lot) => !lot.closed)
      .filter((lot) => lot.name.toLowerCase().includes(query.text.toLowerCase()))
      .filter((lot) => query.category === "All" || lot.category === query.category)
      .sort((a, b) => a.endsAt - b.endsAt);
    const page = found.slice(query.page * PAGE_SIZE, (query.page + 1) * PAGE_SIZE);
    return {
      gold: house.gold,
      terms: TERMS,
      items: house.items.map(({ key, name, used, count }) => ({ key, name, used, count })),
      browse: { text: query.text, category: query.category, page: query.page, more: found.length > (query.page + 1) * PAGE_SIZE, lots: page.map(showLot) },
      selling: house.lots.filter((lot) => lot.mine && !lot.closed).map(showLot),
      bidding: house.lots.filter((lot) => lot.leading && !lot.closed).map(showLot),
      parcels: house.parcels.map(({ id, reason, gold, name, count, used }) => ({ id, reason, gold, name, count, used })),
    };
  }

  const send = () => setTimeout(() => readym.emit("State", JSON.stringify(buildView())), ANSWER_MS);
  const answer = (key, values, done) => setTimeout(() => readym.emit("Answer", fill(auction[`auction.${key}`], values), done), ANSWER_MS);
  const describe = (name, count) => (count === 1 ? name : fill(auction["auction.pieces"], { item: name, count }));
  const findOpen = (id) => house.lots.find((lot) => lot.id === id && !lot.closed);

  const handlers = {
    AuctionReady: () => send(),
    AuctionClose: () => setTimeout(() => readym.emit("Closed"), 0),
    AuctionSearch: (text, category, page) => {
      Object.assign(query, { text, category, page });
      send();
    },
    AuctionPost: (key, count, startPrice, buyoutPrice, hours) => {
      const item = house.items.find((each) => each.key === key);
      const fee = findFee(Math.max(startPrice, buyoutPrice), TERMS.feePercent);
      item.count -= count;
      house.items = house.items.filter((each) => each.count > 0);
      house.gold -= fee;
      house.lots.push({ id: nextId++, name: item.name, count, used: item.used, category: item.category, startPrice: startPrice || null,
        buyoutPrice: buyoutPrice || null, topBid: null, bids: 0, endsAt: Date.now() + hours * HOUR_MS, mine: true, leading: false });
      answer("posted", { item: describe(item.name, count), hours, fee }, true);
      send();
    },
    AuctionBid: (id, amount) => {
      const lot = findOpen(id);
      if (amount < findMinimum(lot)) {
        answer("too_low", { minimum: findMinimum(lot) }, false);
        return;
      }
      Object.assign(lot, { topBid: amount, bids: lot.bids + 1, leading: true });
      house.gold -= amount;
      answer("bid_placed", { amount, item: describe(lot.name, lot.count) }, true);
      send();
    },
    AuctionBuy: (id, price) => {
      const lot = findOpen(id);
      lot.closed = true;
      house.gold -= price;
      house.parcels.push({ id: nextId++, reason: "Bought", gold: 0, name: lot.name, count: lot.count, used: lot.used });
      answer("bought", { item: describe(lot.name, lot.count), price }, true);
      send();
    },
    AuctionWithdraw: (id) => {
      const lot = findOpen(id);
      lot.closed = true;
      house.parcels.push({ id: nextId++, reason: "Withdrawn", gold: 0, name: lot.name, count: lot.count, used: lot.used });
      answer("withdrawn", { item: describe(lot.name, lot.count) }, true);
      send();
    },
    AuctionCollect: (id) => {
      const taken = house.parcels.filter((parcel) => id === 0 || parcel.id === id);
      house.parcels = house.parcels.filter((parcel) => !taken.includes(parcel));
      house.gold += taken.reduce((sum, parcel) => sum + parcel.gold, 0);
      const last = taken.at(-1);
      if (last.gold > 0) answer("collected_gold", { gold: last.gold }, true);
      else answer("collected_item", { item: describe(last.name, last.count) }, true);
      send();
    },
  };

  readym.onSend((name, args) => handlers[name]?.(...args));
}
