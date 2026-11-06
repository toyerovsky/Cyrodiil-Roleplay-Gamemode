import admin from "../../locales/en/admin.json";
import chat from "../../locales/en/chat.json";
import dice from "../../locales/en/dice.json";
import economy from "../../locales/en/economy.json";
import faction from "../../locales/en/faction.json";
import party from "../../locales/en/party.json";
import sanctions from "../../locales/en/sanctions.json";
import staff from "../../locales/en/staff.json";
import trader from "../../locales/en/trader.json";
import { pretendFaction } from "./pretend-faction.js";

const MINUTE_MS = 60_000;
const DAY_MS = 24 * 60 * MINUTE_MS;
const SEPARATOR = " - ";
const ANSWER_MS = 50;
const PLAYED_SECONDS = 26_712;

// The headings the server files its commands under (CommandCategory.cs), and which goes where.
const PLAYER_COMMANDS = {
  roleplay: ["chat.cmd.me", "chat.cmd.do", "chat.cmd.shout", "chat.cmd.whisper", "chat.cmd.try", "dice.cmd.roll", "dice.cmd.board", "dice.cmd.dice"],
  messages: ["chat.cmd.local_ooc", "chat.cmd.global_ooc", "chat.cmd.pm", "party.cmd.party", "faction.cmd.chat"],
  general: ["chat.cmd.players", "chat.cmd.help", "economy.cmd.gold", "faction.cmd.faction"],
};
const DICE_SETS = ["altmer", "argonian", "ayleid", "bosmer", "breton", "daedric", "dunmer", "imperial", "khajiit", "nord", "orc", "redguard"];
const TEXTS = { ...admin, ...chat, ...dice, ...economy, ...faction, ...party, ...sanctions, ...staff, ...trader };
const STAFF_COMMANDS = [
  ...Object.keys(TEXTS).filter((key) => /^(admin|sanctions|staff|trader)\.cmd\./.test(key)),
  "faction.cmd.manage",
];
// The scenarios that open on a tab of their own, and the tab.
const OPENS_ON = { help: "help", account: "account", dice: "dice", lore: "lore", faction: "faction", factionmember: "faction", factionless: "faction" };

function splitUsage(key) {
  const usage = TEXTS[key];
  const at = usage.indexOf(SEPARATOR);
  return { syntax: usage.slice(0, at), description: usage.slice(at + SEPARATOR.length) };
}

const mute = (now) => ({ title: sanctions["sanctions.condition.mute"], reason: "Shouting over a wedding", endsAt: now + 25 * MINUTE_MS });

function group(category, keys) {
  return { category, commands: keys.map(splitUsage).sort((a, b) => a.syntax.localeCompare(b.syntax)) };
}

// Marcus Varro, three days in Cyrodiil. #help opens on the help of an owner, every staff command in it. #account: muted
// for a while, with a second language to pick. #dice: the dice tab, three rolls behind him. #lore: the lore tab, his name
// private, two players online who know him. #faction, #factionmember and #factionless: the faction tab (pretend-faction.js).
export function pretendGame(readym) {
  const scenario = location.hash.slice(1);
  const now = Date.now();
  let language = "en";
  let diceSet = "imperial";
  let namePublic = false;
  const factions = pretendFaction(scenario, now);
  const dice = () => ({
    set: diceSet,
    sets: DICE_SETS,
    recent: [
      { at: now - 4 * MINUTE_MS, asked: "1d20", dice: [{ sides: 20, value: 17 }], total: 17, reason: null },
      { at: now - 40 * MINUTE_MS, asked: "2d6", dice: [{ sides: 6, value: 2 }, { sides: 6, value: 6 }], total: 8, reason: null },
      { at: now - DAY_MS, asked: "5d20", dice: [6, 1, 17, 11, 15].map((value) => ({ sides: 20, value })), total: 50, reason: "Lazy" },
    ],
    maxDice: 5,
  });
  const lore = () => ({
    namePublic,
    header: namePublic ? "Marcus Varro" : "Imperial",
    preview: {
      header: namePublic ? "Marcus Varro" : "Imperial",
      level: "public",
      fields: [
        { id: "race", part: "heritage", label: "Race", text: "Imperial", long: false },
        { id: "build", part: "look", label: "Build", text: "Broad shoulders, a soldier's stance", long: false },
        { id: "hair", part: "look", label: "Hair", text: "Short, greying at the temples", long: false },
        { id: "currently", part: "status", label: "Currently", text: "Guarding a caravan bound for Chorrol, waiting on the merchant.", long: false },
      ],
    },
    known: [
      { number: 3, name: "Lucia Gallus" },
      { number: 12, name: "Dravyn Sadri" },
    ],
  });
  const staffMember = scenario === "help";
  const view = (tab) => ({
    tab,
    character: { firstName: "Marcus", lastName: "Varro", createdAt: now - 3 * DAY_MS, playedSeconds: PLAYED_SECONDS, gold: 1240 },
    account: {
      nickname: "Toyer",
      createdAt: now - 41 * DAY_MS,
      previousVisitAt: now - DAY_MS - 95 * MINUTE_MS,
      sanctions: scenario === "account" ? [mute(now)] : [],
    },
    dice: dice(),
    lore: lore(),
    faction: factions.view(),
    help: [
      ...Object.entries(PLAYER_COMMANDS).map(([category, keys]) => group(category, keys)),
      ...(staffMember ? [group("staff", STAFF_COMMANDS)] : []),
    ],
    languages: scenario === "account" ? ["en", "pl"] : ["en"],
    language,
  });
  const send = (tab) => setTimeout(() => readym.emit("State", JSON.stringify(view(tab))), ANSWER_MS);

  readym.onSend((name, args) => {
    if (name === "MenuReady") send(OPENS_ON[scenario] ?? "character");
    if (name === "MenuFaction") {
      setTimeout(() => {
        const result = factions.run(args[0]);
        readym.emit("Answer", result.text, result.done);
        readym.emit("State", JSON.stringify(view("faction")));
      }, ANSWER_MS);
    }
    if (name === "MenuClose" || name === "MenuDice" || name === "MenuLore") setTimeout(() => readym.emit("Closed"), 0);
    if (name === "MenuLoreName") {
      [namePublic] = args;
      send("lore");
    }
    if (name === "MenuDiceSet") {
      [diceSet] = args;
      send("dice");
    }
    if (name !== "MenuLanguage") return;

    [language] = args;
    readym.emit("Locale", language);
    send("account");
  });
}
