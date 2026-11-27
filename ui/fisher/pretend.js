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

const DAY_MS = 86_400_000;
const TODAY = Date.UTC(2026, 10, 27, 12);
const READER = "Marius Caro";
const landed = (rank, name, lengthCm, grams, daysAgo, mine = false) => ({ rank, name, lengthCm, grams, at: TODAY - daysAgo * DAY_MS, mine });
const mine = (rank, lengthCm, grams, daysAgo) => landed(rank, READER, lengthCm, grams, daysAgo, true);
const board = (tops) => ({
  species: EVERY_SPECIES.map(([, species, name]) => ({ species, name, top: tops[species]?.top ?? [], own: tops[species]?.own ?? null })),
});

// The records of a young server. The reader holds a place among the perch and leads the carp, lies under the five among
// the trout and the pike, and has no spadetail. Strangers are their race, one away is a stranger of no race, two perch
// tie.
const USUAL_RECORDS = board({
  Perch: {
    top: [landed(1, "Jauffre Valtieri", 35, 549, 9), mine(2, 34, 503, 2), landed(3, "Nord", 33, 460, 20), landed(3, "Dar-Ma", 33, 460, 4), landed(5, "Stranger", 31, 381, 1)],
  },
  Spadetail: { top: [landed(1, "Khajiit", 41, 841, 6), landed(2, "Jauffre Valtieri", 38, 669, 15)] },
  Trout: {
    top: [landed(1, "Dar-Ma", 54, 1858, 3), landed(2, "Bosmer", 51, 1565, 11), landed(3, "Jauffre Valtieri", 49, 1388, 30), landed(4, "Stranger", 47, 1225, 8), landed(5, "Nord", 46, 1149, 2)],
    own: mine(12, 38, 647, 5),
  },
  Carp: { top: [mine(1, 66, 4974, 1), landed(2, "Dar-Ma", 61, 3927, 14), landed(3, "Imperial", 44, 1474, 7)] },
  Pike: {
    top: [landed(1, "Bosmer", 108, 10456, 12), landed(2, "Jauffre Valtieri", 101, 8552, 4), landed(3, "Dar-Ma", 97, 7575, 19), landed(4, "Orc", 95, 7116, 2), landed(5, "Stranger", 90, 6051, 25)],
    own: mine(7, 74, 3363, 3),
  },
  Eel: { top: [landed(1, "Argonian", 82, 1406, 10)] },
  Slaughterfish: { top: [landed(1, "Jauffre Valtieri", 104, 12823, 6)] },
});

// The records of a crowded server: five of every species, a name as long as a character's may be, and the reader on the
// board of every third species and far under it in the others.
const LONG_NAME = "Maximilianus-Aurelianus-Septimius-Corvus Vandersteen-Montclair-Albrechtsberger";
const CROWD = ["Jauffre Valtieri", LONG_NAME, "Dunmer", "Dar-Ma", "Stranger"];
const CROWDED_RECORDS = board(Object.fromEntries(EVERY_SPECIES.map(([, species], index) => {
  const longest = 60 + index * 5;
  const top = CROWD.map((name, place) => landed(place + 1, name, longest - place, (longest - place) * 110, place * 3 + index));
  const listed = index % 3 === 0;
  if (listed) top[2] = mine(3, longest - 2, (longest - 2) * 110, 1);
  return [species, { top, own: listed ? null : mine(1204 + index, 21 + index, 870 + index, 40 + index) }];
})));

// The board the fisher answers with when the Records tab asks, null for one that cannot be read. #records: a crowded
// server. #norecords: nobody has caught anything yet. #unread: the board cannot be read. Any other: a young server.
const RECORDS = { records: CROWDED_RECORDS, norecords: board({}), unread: null };

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
  const scenario = location.hash.slice(1);
  const start = (SCENARIOS[scenario] ?? SCENARIOS[""])();
  const records = scenario in RECORDS ? RECORDS[scenario] : USUAL_RECORDS;
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
      case "FisherAskRecords":
        if (records) later("Records", JSON.stringify(records));
        else later("Answer", "The fisher cannot find the book of records just now. Ask again in a minute.", false);
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
