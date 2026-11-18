import texts from "../../locales/en/specialization.json";
import { fill } from "../shared/locale.js";

const ANSWER_MS = 50;
const SECOND_MS = 1000;
const HOUR_MS = 60 * 60 * SECOND_MS;

// The server's defaults: what the first choice and a change cost, the seconds between two (a Release build), and the caps.
const FIRST_PRICE = 0;
const CHANGE_PRICE = 1000;
const COOLDOWN_SECONDS = 60;
const CAPS = { trainedSkill: 60, limitedSkill: 30, trainedAttribute: 75, limitedAttribute: 50, unchosenSkill: 50 };

// The server's specializations, as SpecializationRules has them.
const PROFILES = {
  Knight: { path: "Warrior", mastered: ["Strength", "Endurance", "Blade", "Blunt", "Block", "HeavyArmor", "Armorer"], trained: ["Willpower", "Personality", "HandToHand", "Athletics", "Speechcraft", "Restoration"] },
  Barbarian: { path: "Warrior", mastered: ["Strength", "Endurance", "Blade", "Blunt", "HandToHand", "Athletics", "Acrobatics"], trained: ["Agility", "Luck", "LightArmor", "Block", "Armorer", "Marksman"] },
  Ranger: { path: "Warrior", mastered: ["Agility", "Endurance", "Marksman", "LightArmor", "Sneak", "Athletics", "Acrobatics"], trained: ["Strength", "Willpower", "Blade", "Alchemy", "Security", "Restoration"] },
  Wizard: { path: "Mage", mastered: ["Intelligence", "Willpower", "Destruction", "Alteration", "Mysticism"], trained: ["Personality", "Luck", "Conjuration", "Illusion", "Restoration", "Alchemy", "Speechcraft"] },
  Alchemist: { path: "Mage", mastered: ["Intelligence", "Willpower", "Alchemy", "Restoration", "Mysticism"], trained: ["Personality", "Luck", "Alteration", "Illusion", "Destruction", "Conjuration", "Mercantile"] },
  Illusionist: { path: "Mage", mastered: ["Intelligence", "Personality", "Illusion", "Conjuration", "Speechcraft"], trained: ["Willpower", "Agility", "Mysticism", "Alteration", "Restoration", "Alchemy", "Sneak"] },
  Healer: { path: "Support", mastered: ["Willpower", "Personality", "Restoration", "Alchemy", "Speechcraft"], trained: ["Intelligence", "Endurance", "Mysticism", "Alteration", "Illusion", "Mercantile", "Athletics"] },
  Smith: { path: "Support", mastered: ["Strength", "Endurance", "Armorer", "Mercantile", "Blunt", "HeavyArmor"], trained: ["Personality", "Luck", "Speechcraft", "Block", "Athletics", "Blade", "Security"] },
  Thief: { path: "Support", mastered: ["Agility", "Luck", "Sneak", "Security", "Acrobatics", "LightArmor", "Mercantile"], trained: ["Personality", "Endurance", "Blade", "Marksman", "Speechcraft", "Illusion", "Athletics"] },
};

// The mage's mentor for a character who has not chosen and carries 1,340 gold. #chosen: a Wizard since four days, 2,500
// gold, so a change is allowed. #wait: an Alchemist since 15 s, so a change must wait 45 s more. #registrar: the Imperial
// Registrar, who teaches every path, for a Knight.
const SCENARIOS = {
  "": { path: "Mage", chosen: null, chosenHoursAgo: 0, gold: 1340 },
  chosen: { path: "Mage", chosen: "Wizard", chosenHoursAgo: 96, gold: 2500 },
  wait: { path: "Mage", chosen: "Alchemist", chosenHoursAgo: 15 / 3600, gold: 2500 },
  registrar: { path: null, chosen: "Knight", chosenHoursAgo: 200, gold: 640 },
};

const SKILLS = new Set(["Armorer", "Athletics", "Blade", "Block", "Blunt", "HandToHand", "HeavyArmor", "Alchemy", "Alteration", "Conjuration", "Destruction", "Illusion", "Mysticism", "Restoration", "Acrobatics", "LightArmor", "Marksman", "Mercantile", "Security", "Sneak", "Speechcraft"]);
const MOST_MAJORS = 7;

// As SpecializationRules.FindMajorSkills: every mastered skill, then trained ones, up to 7.
const majorsOf = (profile) => [...profile.mastered, ...profile.trained].filter((stat) => SKILLS.has(stat)).slice(0, MOST_MAJORS);

export function pretendGame(readym) {
  const character = { ...(SCENARIOS[location.hash.slice(1)] ?? SCENARIOS[""]) };
  character.chosenAt = character.chosen ? Date.now() - character.chosenHoursAgo * HOUR_MS : null;
  const offered = () => Object.entries(PROFILES).filter(([, profile]) => !character.path || profile.path === character.path);
  const nextChoiceAt = () => {
    const at = character.chosenAt ? character.chosenAt + COOLDOWN_SECONDS * SECOND_MS : null;
    return at && at > Date.now() ? at : null;
  };
  const view = () => ({
    path: character.path,
    offered: offered().map(([id, profile]) => ({ id, ...profile, majors: majorsOf(profile), chosen: id === character.chosen })),
    chosen: character.chosen,
    gold: character.gold,
    price: character.chosen ? CHANGE_PRICE : FIRST_PRICE,
    nextChoiceAt: nextChoiceAt(),
    caps: CAPS,
  });
  const send = () => setTimeout(() => readym.emit("State", JSON.stringify(view())), ANSWER_MS);
  const name = (id) => texts[`specialization.name.${id.toLowerCase()}`];
  const answer = (key, values, chosen) =>
    setTimeout(() => readym.emit("Answer", fill(texts[`specialization.${key}`], values), chosen), ANSWER_MS);

  readym.onSend((message, args) => {
    if (message === "MentorReady") send();
    if (message === "MentorClose") setTimeout(() => readym.emit("Closed"), 0);
    if (message !== "MentorChoose") return;

    const [id] = args;
    const price = character.chosen ? CHANGE_PRICE : FIRST_PRICE;
    if (price > character.gold) {
      answer("too_poor", { specialization: name(id), price, gold: character.gold }, false);
      return;
    }

    const before = character.chosen;
    character.gold -= price;
    character.chosen = id;
    character.chosenAt = Date.now();
    if (before) answer("changed", { specialization: name(id), price, old: name(before) }, true);
    else answer("chosen", { specialization: name(id) }, true);
    send();
  });
}
