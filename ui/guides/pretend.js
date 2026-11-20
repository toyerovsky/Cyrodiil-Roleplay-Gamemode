import english from "../../locales/en/guides.json";
import polish from "../../locales/pl/guides.json";
import { fill } from "../shared/locale.js";

const ANSWER_MS = 50;
// As GuideCatalogue.Number groups five figures and more.
const GROUPED_FROM = 10_000;
const GROUP = "\u00A0";

// Each guide as the server builds it with the default config (GuideCatalogue.cs): its texts' key, the keys of its steps,
// the numbers its steps take and its facts. #welcome is the card at the first entry, #read a guide read again from the
// menu, a topic's own name (#gambling) that guide at its first use. "" is the bank's. ?locale=pl reads them in Polish.
const TOPICS = {
  Welcome: {
    key: "welcome",
    lines: [["self", ["G"]], ["interact", ["H"]], ["chat", ["K"]], ["answer", ["Y", "N"]], ["menu", ["F10"]], ["dice", ["Home"]], ["lore", ["End"]], ["talk", []]],
  },
  Trader: { key: "trader", steps: 4, facts: [["pieces", "count", 20], ["reach", "metres", 5]] },
  Bank: { key: "bank", steps: 3, facts: [["most", "gold", 1000000], ["fee", "free"]] },
  Stable: { key: "stable", steps: 4, keys: { 3: ["G"] }, facts: [["cheapest", "gold", 500], ["dearest", "gold", 5000]] },
  Mentor: {
    key: "mentor",
    steps: 4,
    args: { 2: { trained: 60, limited: 30 }, 3: { unchosen: 50 } },
    facts: [["first", "free"], ["change", "gold", 1000], ["wait", "days", 3]],
  },
  Foreman: { key: "foreman", steps: 4, facts: [] },
  NoticeBoard: {
    key: "notice_board",
    steps: 4,
    keys: { 1: ["H"] },
    args: { 2: { length: 300 } },
    facts: [["yours", "count", 3], ["wait", "minutes", 5], ["stays", "days", 7], ["shown", "count", 10]],
  },
  Gambling: {
    key: "gambling",
    steps: 5,
    keys: { 1: ["G", "H"], 2: ["Y", "N"], 5: ["Esc"] },
    facts: [["stake", "range", { least: 1, most: 500 }], ["rounds", "count", 2], ["answer", "seconds", 30], ["cut", "percent", 0]],
  },
  Dice: { key: "dice", steps: 4, keys: { 3: ["/roll"], 4: ["F10", "/dice"] }, args: { 2: { count: 5 } }, facts: [["most", "count", 5], ["sets", "count", 12]] },
  Lore: { key: "lore", steps: 4, keys: { 3: ["H"], 4: ["Shift+End"] }, facts: [["answer", "seconds", 30]] },
  Exchange: { key: "exchange", steps: 4, keys: { 1: ["H"] }, facts: [["most", "gold", 100000], ["answer", "seconds", 30]] },
};

const SCENARIOS = {
  "": { topic: "Bank", mode: "Use" },
  welcome: { topic: "Welcome", mode: "Entry" },
  read: { topic: "Stable", mode: "Read" },
  ...Object.fromEntries(Object.keys(TOPICS).filter((topic) => topic !== "Welcome").map((topic) => [topic.toLowerCase(), { topic, mode: "Use" }])),
};

const GUIDES = { en: english, pl: polish };

const text = (guides, key, args) => fill(guides[`guides.${key}`], args);

const number = (amount) => (amount < GROUPED_FROM ? String(amount) : String(amount).replace(/\B(?=(\d{3})+$)/g, GROUP));

function value(guides, locale, kind, amount) {
  if (kind === "free") return text(guides, "value.free");
  if (kind === "range") return text(guides, "value.range", amount);
  if (kind === "days") return fill(guides["guides.value.days"][new Intl.PluralRules(locale).select(amount)], { count: amount });
  return text(guides, `value.${kind}`, { count: kind === "gold" ? number(amount) : amount });
}

export function buildGuide(topic, mode, locale = "en") {
  const guides = GUIDES[locale];
  const guide = TOPICS[topic];
  const steps = guide.lines
    ? guide.lines.map(([line, keys]) => ({ text: text(guides, `${guide.key}.${line}`), keys }))
    : Array.from({ length: guide.steps }, (_, index) => ({
        text: text(guides, `${guide.key}.step.${index + 1}`, guide.args?.[index + 1]),
        keys: guide.keys?.[index + 1] ?? [],
      }));
  return {
    topic,
    mode,
    title: text(guides, `${guide.key}.title`),
    purpose: text(guides, `${guide.key}.purpose`),
    steps,
    facts: (guide.facts ?? []).map(([fact, kind, amount]) => ({ label: text(guides, `${guide.key}.fact.${fact}`), value: value(guides, locale, kind, amount) })),
  };
}

export function pretendGame(readym) {
  const scenario = SCENARIOS[location.hash.slice(1)] ?? SCENARIOS[""];
  const locale = new URLSearchParams(location.search).get("locale") ?? "en";
  readym.onSend((name) => {
    if (name !== "GuideReady") return;
    setTimeout(() => {
      readym.emit("Locale", locale);
      readym.emit("State", JSON.stringify(buildGuide(scenario.topic, scenario.mode, locale)));
    }, ANSWER_MS);
  });
  readym.onSend((name) => {
    if (name === "GuideContinue" || name === "GuideClose") setTimeout(() => readym.emit("Closed"), 0);
  });
}
