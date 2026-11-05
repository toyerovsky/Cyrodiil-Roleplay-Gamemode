import CATALOG from "./pretend-catalog.json";
import FIELDS from "./pretend-fields.json";
import { knowsYou, seesTheirName } from "./card.js";

// The server's PageDesk for the browser preview, after MESOL's own stand-in: a slice of the lore list (the race's
// scope, the 3E 433 era, the homeland's regions, the faction's ranks), two levels of who sees a field, the notes for
// the owner alone, traits typed or rolled with one reroll, and three players around. The real rules are the server's.
//
// Marcus Varro, an Imperial whose name strangers do not see: a few fields filled, Lazy rolled with its reroll left, a
// Dunmer around who wants to meet him. #empty: a card not begun. #view: Lucia Gallus's card, as /lore shows it.
// #nocard: a stranger with no card. #readonly: a card the server cannot save.
const ANSWER_MS = 50;
const NEARBY_RANGE = 20;
const MAX_CARD = 16000;
const TRAIT_DICE = 5;
const D20 = 20;
const TRAIT_MIN = 5;
const TRAIT_MAX = 100;
const ENTRIES = new Map(CATALOG.map((entry) => [entry.id, entry]));
const CHOICE_WORDS = Object.fromEntries(FIELDS.flatMap((field) => (field.choices ?? []).map((choice) => [choice.id, choice.name])));
const IN_HEADER = new Set(["title", "seenAs", "icOoc"]);
const NATIVE = { Imperial: "province.cyrodiil", Khajiit: "province.elsweyr", Nord: "province.skyrim", Dunmer: "province.morrowind" };
const DEPENDENTS = { race: ["subtype", "culture", "upbringing"], homeland: ["homeRegion"] };
const NATIVE_LANDS = "Native lands";
const OTHER_PROVINCES = "Other Tamrielic Provinces";
const WORDS = ["faintly", "somewhat", "notably", "strongly", "overwhelmingly"];
const traitWord = (number) => WORDS[Math.min(WORDS.length, Math.max(1, Math.ceil(number / D20))) - 1];
const rollD20 = () => 1 + Math.floor(Math.random() * D20);

const fieldById = (id) => FIELDS.find((field) => field.id === id);
const isEmpty = (value) => !value || (!value.catalogId && !value.text && value.number == null);
const capital = (word) => word.charAt(0).toUpperCase() + word.slice(1);

function inside(entry, placeId) {
  for (let at = entry, depth = 0; at && depth < 6; depth++) {
    if (at.id === placeId) return true;
    at = at.parent ? ENTRIES.get(at.parent) : null;
  }
  return false;
}

const startingCard = () => ({
  title: { text: "Legion veteran", vis: "private" },
  seenAs: { text: "a broad Imperial in Legion boots", vis: "public" },
  race: { catalogId: "race.imperial", vis: "public" },
  homeland: { catalogId: "province.cyrodiil", vis: "private" },
  class: { catalogId: "class.knight", vis: "private" },
  hair: { text: "Short, greying at the temples", vis: "public" },
  build: { text: "Broad shoulders, a soldier's stance", vis: "public" },
  history: {
    text: "Served twelve years with the Legion at the Gold Coast.\nCame home to Anvil with a bad knee and a good sword.\nNow he guards caravans for whoever pays in coin, not promises.",
    vis: "private",
  },
  notes: { text: "Owes Lucia 40 gold.", vis: "private" },
  icOoc: { text: "ic", vis: "public" },
  "trait.lazy": { number: 50, fixed: false, rolls: [{ dice: [6, 1, 17, 11, 15], total: 50 }], vis: "private" },
  "trait.loyal": { number: 82, fixed: true, vis: "private" },
});

// Each player around: how this player sees them, their relation and their card at each level.
const startingOthers = () => [
  {
    id: "7", meters: 3.2, relation: "knowsYou", race: "Khajiit", name: "Ri'Zahra", incoming: null, outgoing: null,
    fields: { public: [["look", "Seen as", "a scarred Khajiit with a merchant's sash"], ["heritage", "Race", "Khajiit"]], private: [["heritage", "Homeland", "Elsweyr"]] },
  },
  {
    id: "9", meters: 8.4, relation: "known", race: "Imperial", name: "Lucia Gallus", incoming: null, outgoing: null,
    fields: {
      public: [["heritage", "Race", "Imperial"], ["look", "Hair", "Black, braided"]],
      private: [["life", "Affiliation", "Fighters Guild"], ["story", "History", "Born in Chorrol to a family of smiths.\nLeft at sixteen to see the Empire."]],
    },
  },
  {
    id: "12", meters: 15, relation: "stranger", race: "Dunmer", name: "Dravyn Sadri", incoming: "introduce", outgoing: null,
    fields: { public: [["heritage", "Race", "Dunmer"]], private: [["life", "Faith", "The Tribunal"]] },
  },
];

export function pretendGame(readym) {
  const scenario = location.hash.slice(1);
  const card = scenario === "empty" ? {} : startingCard();
  const others = startingOthers();
  let namePublic = false;
  const readOnly = scenario === "readonly";

  const raceName = () => {
    const entry = ENTRIES.get(card.race?.catalogId);
    return entry ? (entry.race ?? entry.name) : null;
  };
  const display = (field, value) => {
    if (field.type === "trait" && value.number != null) return `${value.number} (${traitWord(value.number)}, ${value.rolls?.length ? "rolled" : "chosen"})`;
    if (value.catalogId) return ENTRIES.get(value.catalogId)?.name ?? value.catalogId;
    return field.type === "choice" ? (CHOICE_WORDS[value.text] ?? value.text) : value.text;
  };
  const visOf = (field, value) => (field.locked ? field.vis : (value?.vis ?? field.vis));
  const used = () => Object.values(card).reduce((sum, value) => sum + (value.text?.length ?? 0), 0);
  const header = () => (namePublic ? "Marcus Varro" : (raceName() ?? "Stranger"));

  function values() {
    return Object.fromEntries(
      FIELDS.map((field) => {
        const value = card[field.id];
        const empty = isEmpty(value);
        return [
          field.id,
          {
            catalogId: value?.catalogId ?? null,
            text: value?.text ?? null,
            shown: empty ? "" : display(field, value),
            summary: value?.catalogId ? (ENTRIES.get(value.catalogId)?.summary ?? null) : null,
            number: value?.number ?? null,
            fixed: !!value?.fixed,
            rolls: value?.rolls ?? null,
            vis: visOf(field, value),
            empty,
          },
        ];
      }),
    );
  }

  function allowed(field, entry) {
    if (entry.era === "future" || (entry.era === "historical" && !field.historical)) return false;
    if (entry.modelFit === "implausible" || entry.race === "Mixed") return false;
    if (field.raceScoped && entry.race && entry.race !== raceName()) return false;
    if (field.id === "homeRegion") return !!card.homeland?.catalogId && inside(entry, card.homeland.catalogId);
    if (field.id === "rank") {
      const faction = card.affiliation?.catalogId;
      return (faction && entry.parent === faction) || (entry.kind === "title" && !entry.parent);
    }
    return true;
  }

  function options(field) {
    if (field.needs === "race" && !raceName()) return { ok: true, field: field.id, options: [], message: "Pick your race first." };
    const native = field.id === "homeland" ? NATIVE[raceName()] : null;
    const found = CATALOG.filter((entry) => field.kinds?.includes(entry.kind) && allowed(field, entry))
      .sort((a, b) => Number(b.origin) - Number(a.origin) || a.name.localeCompare(b.name))
      .sort((a, b) => (native ? (a.id === native ? 0 : 1) - (b.id === native ? 0 : 1) : 0))
      .map((entry) => ({
        group: native ? (entry.id === native ? NATIVE_LANDS : OTHER_PROVINCES) : null,
        id: entry.id,
        name: entry.name,
        summary: entry.summary,
        kind: entry.kind,
        race: entry.race,
        origin: entry.origin,
        historical: entry.era === "historical",
      }));
    return { ok: true, field: field.id, options: found, message: found.length ? null : `Nothing in the lore list fits ${field.label} on this card.` };
  }

  // The card as a stranger sees it: the public fields, the race for a name until the name is public.
  // The card as a stranger (public) or someone the player introduced themselves to (private) sees it: the name for the
  // latter, the race for a stranger until the name is public. The notes stay their owner's.
  function view(level) {
    const seen = (field) => !field.ownerOnly && (level === "private" || visOf(field, card[field.id]) === "public");
    const shown = FIELDS.filter((field) => !isEmpty(card[field.id]) && seen(field));
    return {
      header: level === "private" ? "Marcus Varro" : header(),
      level,
      fields: shown
        .filter((field) => !IN_HEADER.has(field.id))
        .map((field) => ({ id: field.id, part: field.part, label: field.label, text: display(field, card[field.id]), long: field.type === "longText" })),
    };
  }

  const done = (ok, message, extra = {}) => ({ ok, message, card: values(), used: used(), ...extra });

  const shownName = (other) => (seesTheirName(other) ? other.name : other.race);
  const viewOf = (other) => {
    const levels = seesTheirName(other) ? ["public", "private"] : ["public"];
    const fields = levels.flatMap((level) => other.fields[level]);
    return { header: shownName(other), level: levels.at(-1), fields: fields.map(([part, label, text]) => ({ id: label, part, label, text, long: text.includes("\n") })) };
  };

  function social(request) {
    if (request.op === "nearby") {
      return {
        ok: true,
        range: NEARBY_RANGE,
        players: others.map((other) => ({
          id: other.id, header: shownName(other), meters: other.meters, relation: other.relation, hasCard: true, incoming: other.incoming, outgoing: other.outgoing,
        })),
      };
    }
    const other = others.find((each) => each.id === request.player);
    if (!other) return { ok: false, message: "That player is not here any more." };
    switch (request.op) {
      case "view":
        return { ok: true, id: other.id, who: shownName(other), relation: other.relation, view: viewOf(other) };
      case "introduce":
        if (knowsYou(other)) return { ok: false, message: `${shownName(other)} knows you already.` };
        if (other.incoming) return social({ op: "accept", player: other.id });
        other.outgoing = "introduce";
        return { ok: true, message: `You offered to introduce yourself to the ${shownName(other)}.` };
      case "accept":
        if (!other.incoming) return { ok: false, message: "They are not waiting for your answer." };
        other.incoming = null;
        other.outgoing = null;
        other.relation = "known";
        return { ok: true, message: `You and ${other.name} know each other now.` };
      case "decline":
        other.incoming = null;
        return { ok: true, message: "Declined." };
      case "forget":
        if (!knowsYou(other)) return { ok: false, message: `${shownName(other)} does not know you.` };
        other.relation = other.relation === "known" ? "youKnow" : "stranger";
        return { ok: true, message: `${other.name} no longer knows you.` };
      default:
        return { ok: false, message: `Unknown request '${request.op}'.` };
    }
  }

  function edit(request) {
    const field = fieldById(request.field);
    if (!field) return { ok: false, message: `There is no field '${request.field}'.` };
    if (request.op === "options") return options(field);
    if (readOnly) return done(false, "Your card cannot be changed right now.");
    const old = card[field.id];
    const vis = old?.vis ?? field.vis;
    switch (request.op) {
      case "set": {
        if (field.type === "catalog") return done(false, `${field.label} is picked from the lore list.`);
        if (field.type === "trait") {
          if (old?.number != null) return done(false, `${field.label} is set for good.`);
          const number = Number(request.text);
          if (!/^\d+$/.test(request.text) || number < TRAIT_MIN || number > TRAIT_MAX)
            return done(false, `${field.label} takes a number from ${TRAIT_MIN} to ${TRAIT_MAX}.`);
          card[field.id] = { number, fixed: true, vis };
          return done(true, `${field.label} is ${number} (${traitWord(number)}), set for good.`);
        }
        const text = field.type === "longText" ? request.text.trim() : request.text.replace(/\s+/g, " ").trim();
        if (field.type === "choice" && !field.choices.some((choice) => choice.id === text)) return done(false, `${field.label} is not one of its choices.`);
        if (text.length > field.max) return done(false, `${field.label} takes at most ${field.max} characters, this is ${text.length}.`);
        card[field.id] = { text, vis };
        return done(true, `${field.label} set.`);
      }
      case "pick": {
        const entry = ENTRIES.get(request.id);
        if (!entry || !field.kinds?.includes(entry.kind) || !allowed(field, entry)) return done(false, `${entry?.name ?? "That"} does not fit ${field.label} on this card.`);
        const before = DEPENDENTS[field.id] ? old?.catalogId : null;
        card[field.id] = { catalogId: entry.id, vis };
        if (before && before !== entry.id) {
          const cleared = DEPENDENTS[field.id].filter((id) => !isEmpty(card[id]));
          for (const id of cleared) card[id] = { vis: card[id].vis };
          if (cleared.length) return done(true, `${field.label} set: ${entry.name}. ${cleared.map((id) => fieldById(id).label).join(" and ")} cleared.`);
        }
        return done(true, `${field.label} set: ${entry.name}.`);
      }
      case "vis":
        if (field.locked) return done(false, `Who sees ${field.label} cannot be changed.`);
        card[field.id] = { ...(old ?? {}), vis: request.vis };
        return done(true, `${field.label} is now ${capital(request.vis)}.`);
      case "clear":
        if (isEmpty(old)) return done(false, `${field.label} is already empty.`);
        if (field.type === "trait") return done(false, `${field.label} is set for good.`);
        card[field.id] = { vis };
        return done(true, `${field.label} cleared.`);
      case "roll": {
        if (field.type !== "trait") return done(false, `${field.label} is not a trait.`);
        if (old?.fixed) return done(false, `${field.label} is set for good: no more rolls.`);
        const dice = Array.from({ length: TRAIT_DICE }, rollD20);
        const total = dice.reduce((sum, value) => sum + value, 0);
        const reroll = old?.number != null;
        card[field.id] = { number: total, fixed: reroll, rolls: [...(old?.rolls ?? []), { dice, total }], vis };
        const message = reroll ? `${field.label}: rerolled ${total}, set for good.` : `${field.label}: rolled ${total}. Keep it, or reroll once.`;
        return done(true, message, { roll: { dice, total, reroll } });
      }
      case "keep":
        if (old?.number == null || old.fixed) return done(false, `${field.label} has no roll to keep.`);
        card[field.id] = { ...old, fixed: true };
        return done(true, `${field.label} kept at ${old.number}, set for good.`);
      default:
        return { ok: false, message: `Unknown request '${request.op}'.` };
    }
  }

  function handle(request) {
    if (["nearby", "view", "introduce", "accept", "decline", "forget"].includes(request.op)) return social(request);
    if (request.op === "load") {
      return {
        ok: true, mode: "canon", readOnly, fields: FIELDS, card: values(), maxCard: MAX_CARD, used: used(),
        raceHint: card.race ? null : { id: "race.imperial", name: "Imperial" }, name: "Marcus Varro", namePublic, header: header(),
      };
    }
    if (request.op === "preview") return { ok: true, view: view(request.level) };
    if (request.op === "namevis") {
      if (readOnly) return { ok: false, message: "Your card cannot be changed right now." };
      namePublic = request.vis === "public";
      return { ok: true, message: namePublic ? "Everyone sees your name now." : "Strangers see only your race now.", namePublic, header: header() };
    }
    return edit(request);
  }

  const push = (message) => setTimeout(() => readym.emit("Answer", 0, JSON.stringify(message)), ANSWER_MS);

  readym.onSend((name, args) => {
    if (name === "LoreReady") {
      if (scenario === "view") push({ op: "view", who: "Lucia Gallus", view: viewOf(others[1]) });
      else if (scenario === "nocard") push({ op: "view", who: "Bosmer", view: null });
      else push({ op: "edit" });
    }
    if (name === "LoreClose") setTimeout(() => readym.emit("Closed"), 0);
    if (name !== "LoreAsk") return;
    const [requestId, json] = args;
    setTimeout(() => readym.emit("Answer", requestId, JSON.stringify(handle(JSON.parse(json)))), ANSWER_MS);
  });
}
