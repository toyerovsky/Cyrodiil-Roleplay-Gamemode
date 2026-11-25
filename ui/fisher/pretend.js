const GOLD = 120;
const POOR = 3;
const MOST_PIECES = 20;
const ANSWER_MS = 50;

const fish = (key, species, name, price, count, trophy = false) => ({
  item: trophy ? `${key}.trophy` : key,
  species,
  name: trophy ? `Trophy ${name}` : name,
  trophy,
  count,
  price: trophy ? price * 3 : price,
});
const CATCH = [
  fish("fishing.perch", "Perch", "Silverside Perch", 2, 5),
  fish("fishing.pike", "Pike", "Pike", 7, 1, true),
  fish("fishing.carp", "Carp", "Rumare Carp", 4, 2),
  fish("fishing.eel", "Eel", "Niben Eel", 5, 1),
];
const EVERY_SPECIES = [
  ["fishing.perch", "Perch", "Silverside Perch", 2],
  ["fishing.betty", "Betty", "River Betty", 1],
  ["fishing.spadetail", "Spadetail", "Cyrodilic Spadetail", 3],
  ["fishing.trout", "Trout", "Colovian Trout", 4],
  ["fishing.carp", "Carp", "Rumare Carp", 4],
  ["fishing.pike", "Pike", "Pike", 7],
  ["fishing.eel", "Eel", "Niben Eel", 5],
  ["fishing.histcarp", "Histcarp", "Histcarp", 4],
  ["fishing.longfin", "Longfin", "Abecean Longfin", 6],
  ["fishing.seabass", "Seabass", "Topal Sea Bass", 5],
  ["fishing.slaughterfish", "Slaughterfish", "Slaughterfish", 10],
  ["fishing.catfish", "Catfish", "Glass Catfish", 14],
  ["fishing.goldencarp", "GoldenCarp", "Emperor's Golden Carp", 60],
];
const rod = (ware, icon, name, price, weight, reachCm, line) => ({ ware, kind: "Rod", icon, name, price, weight, reachCm, line, count: null });
const bait = (ware, kind, icon, name, price, count) => ({ ware, kind, icon, name, price, weight: 0.1, reachCm: null, line: null, count });
const SHOP = [
  rod("fishing.rod", "rod", "Fishing Rod", 25, 3, 700, 1),
  rod("fishing.rod.sturdy", "rod-sturdy", "Sturdy Fishing Rod", 120, 3.5, 1000, 1.25),
  rod("fishing.rod.master", "rod-master", "Master Fishing Rod", 450, 4, 1300, 1.5),
  bait("fishing.worms", "Bait", "worms", "Fishing Worms", 1, 12),
  bait("fishing.dough", "Bait", "dough", "Dough Bait", 2, 0),
  bait("fishing.lure", "Lure", "spinner", "Spoon Lure", 15, 1),
];
const REPAIRS = [
  { item: "fishing.rod.worn", icon: "rod", name: "Fishing Rod (Worn)", condition: "Worn", price: 4 },
  { item: "fishing.rod.sturdy.damaged", icon: "rod-sturdy", name: "Sturdy Fishing Rod (Damaged)", condition: "Damaged", price: 36 },
  { item: "fishing.rod.master.broken", icon: "rod-master", name: "Master Fishing Rod (Broken)", condition: "Broken", price: 203 },
];

// The fisher with a catch of four kinds, the shop and three rods to mend, 120 gold. #empty: nothing to sell, nothing
// for sale, nothing to mend. #poor: 3 gold. #many: every species and its trophy, a long list, more perch than one deal
// takes.
const SCENARIOS = {
  "": () => ({ gold: GOLD, catch: CATCH, shop: SHOP, repairs: REPAIRS }),
  empty: () => ({ gold: GOLD, catch: [], shop: [], repairs: [] }),
  poor: () => ({ gold: POOR, catch: CATCH, shop: SHOP, repairs: REPAIRS }),
  many: () => ({
    gold: GOLD,
    catch: EVERY_SPECIES.flatMap(([key, species, name, price], index) => [fish(key, species, name, price, index === 0 ? 34 : index + 2), fish(key, species, name, price, 1, true)]),
    shop: SHOP,
    repairs: REPAIRS,
  }),
};

export function pretendGame(readym) {
  const start = (SCENARIOS[location.hash.slice(1)] ?? SCENARIOS[""])();
  let gold = start.gold;
  let catches = start.catch.map((row) => ({ ...row }));
  let repairs = [...start.repairs];
  const shop = start.shop.map((row) => ({ ...row }));
  const later = (name, ...args) => setTimeout(() => readym.emit(name, ...args), ANSWER_MS);
  const send = () => later("State", JSON.stringify({ gold, most: MOST_PIECES, catch: catches, shop, repairs }));
  const sell = (item, count) => {
    const row = catches.find((each) => each.item === item);
    gold += row.price * count;
    row.count -= count;
    catches = catches.filter((each) => each.count > 0);
    later("Answer", `Sold ${row.name} × ${count} for ${row.price * count} gold.`, true);
    send();
  };

  readym.onSend((name, args) => {
    switch (name) {
      case "FisherReady":
        send();
        break;
      case "FisherClose":
        setTimeout(() => readym.emit("Closed"), 0);
        break;
      case "FisherSell":
        sell(...args);
        break;
      case "FisherSellAll": {
        const worth = catches.reduce((sum, row) => sum + row.price * row.count, 0);
        gold += worth;
        catches = [];
        later("Answer", `Sold the whole catch for ${worth} gold.`, true);
        send();
        break;
      }
      case "FisherBuy": {
        const [ware, count] = args;
        const row = shop.find((each) => each.ware === ware);
        if (row.price * count > gold) {
          later("Answer", `That costs ${row.price * count} gold and you have ${gold}.`, false);
          break;
        }
        gold -= row.price * count;
        if (row.count !== null) row.count += count;
        later("Answer", `Bought ${row.name} × ${count} for ${row.price * count} gold.`, true);
        send();
        break;
      }
      case "FisherRepair": {
        const row = repairs.find((each) => each.item === args[0]);
        if (row.price > gold) {
          later("Answer", `Mending it costs ${row.price} gold and you have ${gold}.`, false);
          break;
        }
        gold -= row.price;
        repairs = repairs.filter((each) => each !== row);
        later("Answer", `The fisher mended your ${row.name} for ${row.price} gold.`, true);
        send();
        break;
      }
    }
  });
}
