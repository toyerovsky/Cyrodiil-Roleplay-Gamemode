// The card's sections in their order (the server's CardPart), the last one the players around.
export const PARTS = ["identity", "heritage", "life", "look", "story", "status", "traits", "nearby"];
export const NEARBY = "nearby";
export const IDENTITY = "identity";

export const PUBLIC = "public";
export const PRIVATE = "private";
// A field only its owner reads (notes), locked so.
export const OWNER = "owner";

export const TRAIT_MIN = 5;
export const TRAIT_MAX = 100;
const TRAIT_WORD_STEP = 20;
const TRAIT_WORDS = 5;

// The field another field waits for: subtype and culture need the race, the home region the homeland. The rank's
// affiliation only narrows it, so a rank can be picked without one.
export const OPTIONAL_NEED = "affiliation";

export const emptyValue = (field) => ({ empty: true, vis: field.vis });

// What a field's tag says: who sees it, or that only its owner does.
export const visibilityOf = (field, value) => (field.ownerOnly ? OWNER : (value?.vis ?? field.vis));

// Between this player and one around (the server's LoreNearbyPlayerJson): "known" both ways, "knowsYou" (they know this
// player's name), "youKnow" (this player knows theirs) or "stranger".
export const KNOWN = "known";
export const RELATIONS = ["stranger", KNOWN, "knowsYou", "youKnow"];
export const relationOf = (person) => (RELATIONS.includes(person?.relation) ? person.relation : "stranger");
export const seesTheirName = (person) => [KNOWN, "youKnow"].includes(relationOf(person));
export const knowsYou = (person) => [KNOWN, "knowsYou"].includes(relationOf(person));
export const mayIntroduce = (person) => !person.incoming && !person.outgoing && !knowsYou(person);

export const otherVisibility = (vis) => (vis === PUBLIC ? PRIVATE : PUBLIC);

// none, rolled (a reroll left), fixed
export function traitState(value) {
  if (value?.number == null) return "none";
  return value.fixed ? "fixed" : "rolled";
}

// 1 to 5, the word for how strongly a trait drives the character (the server's CardFields.TraitWord).
export const traitWord = (number) => Math.min(TRAIT_WORDS, Math.max(1, Math.ceil(number / TRAIT_WORD_STEP)));

// Accents and case do not count in a search.
export const fold = (text) =>
  String(text ?? "")
    .normalize("NFD")
    .replace(/[̀-ͯ]/g, "")
    .toLowerCase();

// Names that start with the search first, then those that hold it in their kind, race or group.
export function filterOptions(options, query, kindName) {
  const wanted = fold(query).trim();
  if (!wanted) return options;
  const starts = options.filter((option) => fold(option.name).startsWith(wanted));
  const holds = options.filter(
    (option) =>
      !fold(option.name).startsWith(wanted) && fold(`${option.name} ${kindName(option.kind) ?? ""} ${option.race ?? ""} ${option.group ?? ""}`).includes(wanted),
  );
  return [...starts, ...holds];
}

// A card view's fields under their sections, in the card's order.
export const groupView = (fields) =>
  PARTS.map((part) => ({ part, fields: fields.filter((field) => field.part === part) })).filter((group) => group.fields.length);
