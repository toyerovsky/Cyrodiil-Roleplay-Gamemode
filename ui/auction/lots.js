import { fill } from "../shared/locale.js";

const WHOLE = /^\d+$/;
const MINUTE_MS = 60_000;
const MINUTES_IN_HOUR = 60;
const PERCENT = 100;

// The kinds of goods in the window's order (AuctionCategory.cs).
export const CATEGORIES = ["All", "Weapons", "Armour", "Clothing", "Potions", "Ingredients", "Books", "Other"];

// The listing fee as AuctionRules.Fee has it: a share rounded up, and at least 1 gold when there is a fee at all.
export const findFee = (price, percent) => (percent === 0 ? 0 : Math.max(1, Math.floor((price * percent + PERCENT - 1) / PERCENT)));

// A typed whole sum: 0 for an empty field, null for anything else that is not a whole number.
export function readSum(text) {
  const trimmed = text.trim();
  if (trimmed === "") return 0;
  return WHOLE.test(trimmed) ? Number(trimmed) : null;
}

// An item's name as the window shows it: how many, and whether it is worn down.
export function describeItem(t, item) {
  const name = item.used ? fill(t.used, { name: item.name }) : item.name;
  return item.count > 1 ? `${name} ${fill(t.times, { count: item.count })}` : name;
}

// The time a lot has left in whole minutes, rounded up.
export function describeLeft(t, ms) {
  if (ms <= 0) return t.left_soon;
  const minutes = Math.ceil(ms / MINUTE_MS);
  if (minutes < MINUTES_IN_HOUR) return fill(t.left_minutes, { minutes });
  return fill(t.left_hours, { hours: Math.floor(minutes / MINUTES_IN_HOUR), minutes: minutes % MINUTES_IN_HOUR });
}
