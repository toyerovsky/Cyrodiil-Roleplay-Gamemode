import exchange from "../../locales/en/exchange.json";
import { fill } from "../shared/locale.js";

const CARRIED = 120;
const CONFIRM_DELAY_MS = 3000;
const ANSWER_MS = 50;
const LUCIA_CONFIRMS_MS = 600;
const DONE_SHOWS_MS = 2000;
const LUCIA_OFFERS = 40;

const item = (name, count, used = false) => ({ key: name, name, used, count });

// Enough to scroll, with the longest name of the game and two stacks of one sword.
const ITEMS = [
  item("Apple", 4),
  item("Bone", 2),
  item("Bread Loaf", 5),
  item("Cheese Wedge", 3),
  item("Cloth", 6),
  item("Common Soul Gem", 1),
  item("Cursed Gauntlets of Resilient Flesh", 1),
  item("Fur Boots", 1),
  item("Iron Arrow", 60),
  item("Iron Dagger", 1),
  item("Iron Longsword", 1),
  item("Iron Longsword", 1, true),
  item("Iron Ore", 8),
  item("Leather Cuirass", 1),
  item("Lockpick", 12),
  item("Potion of Healing", 3),
  item("Repair Hammer", 2),
  item("Silver Nugget", 2),
  item("Steel Dagger", 1),
  item("Tongs", 1),
  item("Torch", 2),
  item("Wolf Pelt", 3),
  item("Yarn", 7),
].map((entry) => ({ ...entry, key: entry.used ? `${entry.name}, used` : entry.name }));

const LUCIA_ITEMS = [item("Bread Loaf", 3), item("Steel Longsword", 1, true), item("Resist Normal Weapons Quilted Doublet", 1)];

// The trade with Lucia Gallus, the server and Lucia both played here. She confirms once the player has. #offered: she
// has already put 40 gold and three items in and confirmed. #bare: the player carries nothing to trade. T opens the
// window again after it closed.
export function pretendGame(readym) {
  const scenario = location.hash.slice(1);
  const carried = scenario === "bare" ? [] : ITEMS;
  let deal = null;

  const schedule = (ms, run) => setTimeout(run, ms);
  const view = () => ({
    version: deal.version,
    other: "Lucia Gallus",
    carried: CARRIED,
    max: CARRIED,
    confirmInMs: Math.max(0, deal.changedAt + CONFIRM_DELAY_MS - Date.now()),
    items: carried,
    mine: { gold: deal.mine, items: deal.mineItems, confirmed: deal.mineConfirmed },
    theirs: { gold: deal.theirs, items: deal.theirItems, confirmed: deal.theirsConfirmed },
  });
  const send = () => schedule(ANSWER_MS, () => readym.emit("State", JSON.stringify(view())));
  const markChanged = () => {
    deal.version++;
    deal.changedAt = Date.now();
    deal.mineConfirmed = false;
    deal.theirsConfirmed = false;
  };

  const open = () => {
    const offered = scenario === "offered";
    deal = {
      version: 1,
      changedAt: Date.now(),
      mine: 0,
      mineItems: [],
      theirs: offered ? LUCIA_OFFERS : 0,
      theirItems: offered ? LUCIA_ITEMS : [],
      mineConfirmed: false,
      theirsConfirmed: offered,
    };
    send();
  };

  const countPieces = (items) => items.reduce((sum, each) => sum + each.count, 0);
  const describe = (key, count) => fill(exchange[key][count === 1 ? "one" : "other"], { count });

  const finish = () => {
    const received = deal.theirs - deal.mine;
    const parts = [];
    if (received > 0) parts.push(describe("exchange.part.gold_received", received));
    if (received < 0) parts.push(describe("exchange.part.gold_gave", -received));
    if (deal.theirItems.length > 0) parts.push(describe("exchange.part.items_received", countPieces(deal.theirItems)));
    if (deal.mineItems.length > 0) parts.push(describe("exchange.part.items_gave", countPieces(deal.mineItems)));
    const text = parts.length > 0 ? fill(exchange["exchange.result"], { what: parts.join(", ") }) : exchange["exchange.even"];
    deal = null;
    readym.emit("Done", text);
    schedule(DONE_SHOWS_MS, () => readym.emit("Closed"));
  };

  const offerItem = (key, count) => {
    const held = carried.find((each) => each.key === key);
    if (!held) {
      readym.emit("Refused", exchange["exchange.item_gone"]);
      send();
      return;
    }
    if (count < 0 || count > held.count) {
      readym.emit("Refused", fill(exchange["exchange.item_amount"], { max: held.count, item: held.name }));
      send();
      return;
    }
    const others = deal.mineItems.filter((each) => each.key !== key);
    deal.mineItems = count > 0 ? [...others, { ...held, count }] : others;
    deal.mineItems.sort((one, other) => one.key.localeCompare(other.key));
    markChanged();
    send();
  };

  readym.onSend((name, args) => {
    if (name === "ExchangeReady") open();
    if (!deal) return;

    if (name === "ExchangeOffer") {
      const [gold] = args;
      if (gold < 0 || gold > CARRIED) {
        readym.emit("Refused", fill(exchange["exchange.amount"], { max: CARRIED }));
        send();
        return;
      }
      if (gold === deal.mine) return;
      deal.mine = gold;
      markChanged();
      send();
    } else if (name === "ExchangeOfferItem") {
      offerItem(...args);
    } else if (name === "ExchangeConfirm") {
      const [version] = args;
      if (version !== deal.version) {
        readym.emit("Refused", exchange["exchange.changed"]);
        send();
        return;
      }
      deal.mineConfirmed = true;
      if (deal.theirsConfirmed) {
        finish();
        return;
      }
      send();
      schedule(LUCIA_CONFIRMS_MS, () => {
        if (deal?.mineConfirmed) finish();
      });
    } else if (name === "ExchangeCancel") {
      deal = null;
      readym.emit("Closed");
    }
  });

  document.addEventListener("keydown", (event) => {
    if (deal || event.key.toLowerCase() !== "t") return;
    event.preventDefault();
    open();
  });
}
