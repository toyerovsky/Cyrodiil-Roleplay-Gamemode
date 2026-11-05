import admin from "../../locales/en/admin.json";
import sanctionTexts from "../../locales/en/sanctions.json";
import spawnTexts from "../../locales/en/spawn.json";
import staff from "../../locales/en/staff.json";
import { fill } from "../shared/locale.js";

const ANSWER_MS = 50;
const MINUTE_MS = 60_000;
const DAY_MS = 24 * 60 * MINUTE_MS;
const AUDIT_PAGE = 25;
const AUDIT_ENTRIES = 40;
const ROLL_PAGE = 25;
const ROLL_COUNT = 34;
const ROLLERS = ["Marius Varro", "Lucia Gallus", "Brannoc", "Elsia Ramaryn"];
const ROLL_AREAS = ["L_ICMarketDistrict", "L_Tamriel", "L_PersistentDungeon-2C171"];
const SPAWN = { x: 25610, y: -91240, z: 1215 };
const SPAWN_HERE = { x: 26002, y: -90418, z: 1198 };
const ALL_COMMANDS = [
  "duty", "aheal", "ahp", "arevive", "agoto", "abring", "aback", "atp", "afreeze", "aunfreeze", "amute", "aunmute", "awarn", "akick",
  "aban", "aunban", "agold", "aitem", "aroles", "atime", "aweather", "aspawn", "amsg",
];
const ADMIN_COMMANDS = ["agold", "aitem", "aroles", "atime", "aweather", "aban", "aunban", "aspawn"];
const SELF = 1;
const MODERATOR_COMMANDS = ALL_COMMANDS.filter((command) => !ADMIN_COMMANDS.includes(command));

// A player with a character, outside, healthy and free, unless the changes say otherwise.
const makePlayer = (number, name, nickname, changes = {}) => ({
  number, name, nickname, roles: [], character: true, muted: false, frozen: false, area: "L_Tamriel", health: 80, healthMax: 80,
  ...changes,
});
const AWAY = { area: null, health: null, healthMax: null };
const CROWD = ["Aelia Sabina", "Glarthir", "Hrodulf the Bold", "Elsia Ramaryn", "Valus Odiil", "Sinderion", "Ra'jhan", "Martin Septim"];

const PLAYERS = [
  makePlayer(1, "Marcus Varro", "Toyer", { roles: ["owner"], health: 120, healthMax: 120 }),
  makePlayer(3, "Lucia Gallus", "lucia_g", { muted: true, health: 64, healthMax: 90 }),
  makePlayer(4, "Brannoc the Unlucky Wanderer of the Gold Coast", "brannoc1987_long_nickname", {
    frozen: true,
    area: "L_PersistentDungeon-1A2B3",
    health: 0,
    healthMax: 140,
  }),
  makePlayer(7, "J'zargo", "jz", { roles: ["moderator"], ...AWAY }),
  makePlayer(9, "[9]", "newcomer", { character: false, ...AWAY }),
  ...CROWD.map((name, index) => makePlayer(10 + index, name, `player${10 + index}`)),
];

const PLACES = [
  ["anvil-north-gate", "Anvil, north gate", "gate"], ["bravil", "Bravil", "gate"], ["bruma-east-gate", "Bruma, east gate", "gate"],
  ["bruma-north-gate", "Bruma, north gate", "gate"], ["chorrol-north-gate", "Chorrol, north gate", "gate"],
  ["imperial-city", "Imperial City", "gate"], ["kvatch", "Kvatch", "gate"], ["leyawiin-east-gate", "Leyawiin, east gate", "gate"],
  ["skingrad-west-gate", "Skingrad, west gate", "gate"],
  ["anvil-lighthouse", "Anvil Lighthouse", "city"], ["imperial-city-waterfront", "Imperial City Waterfront", "city"],
  ["weye", "Weye", "settlement"], ["pells-gate", "Pell's Gate", "settlement"], ["aleswell", "Aleswell", "settlement"],
  ["hackdirt", "Hackdirt", "settlement"], ["cropsford", "Cropsford", "settlement"], ["harluns-watch", "Harlun's Watch", "settlement"],
  ["roxey-inn", "Roxey Inn", "inn"], ["the-drunken-dragon-inn", "The Drunken Dragon Inn", "inn"], ["wawnet-inn", "Wawnet Inn", "inn"],
].map(([id, name, kind]) => ({ id, name, kind }));

const ITEMS = [
  ["WeapIronLongsword", "Iron Longsword", "WEAP"], ["ArenaIronLongsword", "Iron Longsword", "WEAP"],
  ["WeapSteelLongsword", "Steel Longsword", "WEAP"], ["WeapElvenLongsword", "Elven Longsword", "WEAP"],
  ["WeapIronDagger", "Iron Dagger", "WEAP"], ["WeapSteelWarAxe", "Steel War Axe", "WEAP"], ["WeapIronBow", "Iron Bow", "WEAP"],
  ["ArmorIronCuirass", "Iron Cuirass", "ARMO"], ["ArmorLeatherBoots", "Leather Boots", "ARMO"],
  ["ClothesShirt01", "Coarse Linen Shirt", "CLOT"], ["Lockpick", "Lockpick", "MISC"], ["Torch02", "Torch", "LIGH"],
  ["Breadloaf", "Bread Loaf", "INGR"], ["PotionRestoreHealthS", "Potion of Healing", "ALCH"],
  ["Arrow1Iron", "Iron Arrow", "AMMO"], ["SoulGem5GrandEmpty", "Grand Soul Gem", "SLGM"],
].map(([id, name, type]) => ({ id, name, type, count: 0, worn: false }));
const SEARCH_FROM = 2;

// Brannoc's freeze, on his card and among the sanctions.
const FROZEN = {
  kind: "admin.freeze", nickname: "brannoc1987_long_nickname", character: "Brannoc the Unlucky Wanderer of the Gold Coast",
  until: null, reason: "Running from the guards mid scene", online: 4,
};

const INVENTORY = [
  { id: "0001A2B3", name: "0001A2B3", type: "", count: 1, worn: false },
  { id: "ClothesShirt01", name: "Coarse Linen Shirt", type: "CLOT", count: 1, worn: true },
  { id: "ClothesPants01", name: "Coarse Linens", type: "CLOT", count: 1, worn: true },
  { id: "Gold001", name: "Gold", type: "MISC", count: 1240, worn: false },
  { id: "Lockpick", name: "Lockpick", type: "MISC", count: 12, worn: false },
  { id: "Torch02", name: "Torch", type: "LIGH", count: 2, worn: false },
];

// The owner on duty with every tab, and the players online. #card opens on Brannoc, frozen in a dungeon with a long name.
// #offduty is a moderator off duty: the buttons wait for the duty. #sanctions, #audit, #world and #items open on their
// tab, #dice on the roll log. The staff member stands outside, where the trader is.
export function pretendGame(readym) {
  const scenario = location.hash.slice(1);
  const now = Date.now();
  const moderator = scenario === "offduty";
  let onDuty = !moderator;
  // #nospawn: none set yet, until Set it where I stand.
  let spawn = scenario === "nospawn" ? null : SPAWN;
  const emitLater = (name, ...args) => setTimeout(() => readym.emit(name, ...args), ANSWER_MS);

  const state = (focus = null) => ({
    tab: ["sanctions", "audit", "world", "items", "dice"].includes(scenario) ? scenario : scenario === "nospawn" ? "world" : "players",
    focus,
    self: SELF,
    area: "L_Tamriel",
    acting: onDuty,
    onDuty,
    tabs: moderator ? ["players", "sanctions", "audit", "world", "dice"] : ["players", "items", "sanctions", "audit", "world", "dice"],
    allowed: moderator ? MODERATOR_COMMANDS : ALL_COMMANDS,
    players: PLAYERS,
    places: PLACES,
    weathers: ["clear", "cloudy", "overcast", "fog", "rain", "thunderstorm", "snow"],
    traders: [
      { id: 1, shop: "general", area: "L_Tamriel", x: 25444, y: -91022, z: 1210 },
      { id: 2, shop: "general", area: "L_ICMarketDistrict", x: 1220, y: 840, z: 96 },
    ],
    spawn,
  });

  const actions = ["admin.heal", "admin.mute", "admin.teleport", "staff.message", "admin.freeze"];
  const details = {
    "admin.mute": "minutes=30, reason=Shouting over the wedding at the chapel, again and again",
    "admin.teleport": "to=weye",
  };
  // Newest first: a d20 now and then, two d6, and a lore card's trait rolled with five d20.
  const rolls = Array.from({ length: ROLL_COUNT }, (_, index) => {
    const dice =
      index % 5 === 2
        ? [6, 1, 17, 11, 15].map((value) => ({ sides: 20, value }))
        : index % 3 === 1
          ? [{ sides: 6, value: 1 + (index % 6) }, { sides: 6, value: 6 - (index % 6) }]
          : [{ sides: 20, value: 1 + ((index * 7) % 20) }];
    return {
      id: ROLL_COUNT - index,
      at: now - index * 11 * MINUTE_MS,
      character: ROLLERS[index % ROLLERS.length],
      asked: "",
      dice,
      total: dice.reduce((sum, die) => sum + die.value, 0),
      reason: index % 5 === 2 ? "Lazy" : null,
      area: ROLL_AREAS[index % ROLL_AREAS.length],
    };
  });

  const entries = Array.from({ length: AUDIT_ENTRIES }, (_, index) => ({
    id: 1000 - index,
    at: now - index * 37 * MINUTE_MS,
    action: actions[index % actions.length],
    actor: "Marcus Varro (Toyer)",
    target: index % 4 === 3 ? null : "Brannoc the Unlucky Wanderer of the Gold Coast (brannoc1987_long_nickname)",
    details: details[actions[index % actions.length]] ?? null,
  }));

  const card = (number) => {
    const player = PLAYERS.find((each) => each.number === number);
    return {
      number,
      name: player.name,
      nickname: player.nickname,
      account: "3f2b9d4e-8a1c-4c6e-9b7a-1d2e3f4a5b6c",
      accountCreatedAt: now - 41 * DAY_MS,
      previousVisitAt: now - DAY_MS,
      characterCreatedAt: player.character ? now - 3 * DAY_MS : null,
      playedSeconds: 26_712,
      area: player.area,
      position: player.area ? "25444 -91022 1210" : null,
      sanctions: player.frozen ? [{ ...FROZEN, account: "3f2b9d4e", by: "Marcus Varro (Toyer)", since: now - 9 * MINUTE_MS }] : [],
      history: entries.slice(0, 6),
      inventory: player.area ? INVENTORY : null,
    };
  };

  const sanctions = [
    {
      kind: "ban", account: "a1b2c3d4-0000-4000-8000-000000000001", nickname: "griefer42", character: "Nobody Special",
      since: null, until: null, reason: "Killing new players at the gate, three times", by: null, online: null,
    },
    {
      kind: "admin.mute", account: "a1b2c3d4-0000-4000-8000-000000000003", nickname: "lucia_g", character: "Lucia Gallus",
      since: now - 5 * MINUTE_MS, until: now + 25 * MINUTE_MS, reason: "Shouting over a wedding", by: "Marcus Varro (Toyer)", online: 3,
    },
    { ...FROZEN, account: "a1b2c3d4-0000-4000-8000-000000000004", by: "J'zargo (jz)", since: now - 9 * MINUTE_MS },
  ];

  const answers = {
    duty: () => (onDuty ? staff["staff.duty_on"] : fill(staff["staff.duty_off"].other, { count: 42 })),
    aheal: (name) => fill(admin["admin.healed"], { name }),
    afreeze: (name) => fill(sanctionTexts["sanctions.frozen"], { name }),
    awarn: (name) => fill(sanctionTexts["sanctions.warned"], { name }),
    atp: (place) => fill(admin["admin.teleported_to"], { place }),
    aspawn: (what) => (what === "go" ? spawnTexts["spawn.went"] : spawnTexts["spawn.set"]),
  };

  readym.onSend((name, args) => {
    if (name === "AdminReady") emitLater("State", JSON.stringify(state(scenario === "card" ? 4 : null)));
    if (name === "AdminAsk") {
      const [kind, argument] = args;
      if (kind === "card") emitLater("Part", kind, JSON.stringify(card(Number(argument))));
      if (kind === "sanctions") emitLater("Part", kind, JSON.stringify(sanctions));
      if (kind === "items") {
        const { text, type } = JSON.parse(argument);
        const wanted = text.toLowerCase();
        const found = ITEMS.filter(
          (item) => (!type || item.type === type) && (wanted.length < SEARCH_FROM || `${item.id} ${item.name}`.toLowerCase().includes(wanted)),
        );
        emitLater("Part", kind, JSON.stringify(found.sort((a, b) => a.name.localeCompare(b.name))));
      }
      if (kind === "rolls") {
        const { player, before } = JSON.parse(argument);
        const wanted = (player ?? "").toLowerCase();
        const matching = rolls.filter((roll) => roll.character.toLowerCase().includes(wanted));
        const from = before ? matching.findIndex((roll) => roll.id < before) : 0;
        const page = matching.slice(from, from + ROLL_PAGE);
        emitLater("Part", kind, JSON.stringify({ rolls: page, more: from + ROLL_PAGE < matching.length, appended: !!before }));
      }
      if (kind === "audit") {
        const { before } = JSON.parse(argument);
        const from = before ? entries.findIndex((entry) => entry.id < before) : 0;
        const page = entries.slice(from, from + AUDIT_PAGE);
        emitLater("Part", kind, JSON.stringify({ entries: page, more: from + AUDIT_PAGE < entries.length, appended: !!before }));
      }
    }
    if (name === "AdminRun") {
      const [line] = args;
      const [command, , ...rest] = line.slice(1).split(" ");
      if (command === "duty") onDuty = !onDuty;
      if (line === "/aspawn here") spawn = SPAWN_HERE;
      const who = PLAYERS.find((player) => line.split(" ")[1] === String(player.number))?.name ?? rest.join(" ");
      const text = answers[command]?.(["atp", "aspawn"].includes(command) ? line.split(" ")[1] : who) ?? `${line}: done`;
      emitLater("Answer", text, true);
      emitLater("State", JSON.stringify(state()));
    }
  });
}
