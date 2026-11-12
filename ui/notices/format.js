const MINUTES_IN_HOUR = 60;
const MINUTES_IN_DAY = 24 * MINUTES_IN_HOUR;

// How long ago, in the reader's language: "now", "5 minutes ago", "2 days ago".
export function formatAge(minutes, locale) {
  const relative = new Intl.RelativeTimeFormat(locale, { numeric: "auto" });
  if (minutes < 1) return relative.format(0, "second");
  if (minutes < MINUTES_IN_HOUR) return relative.format(-minutes, "minute");
  if (minutes < MINUTES_IN_DAY) return relative.format(-Math.floor(minutes / MINUTES_IN_HOUR), "hour");
  return relative.format(-Math.floor(minutes / MINUTES_IN_DAY), "day");
}

// A span with its unit in the reader's language: "7 days", "4 minuty".
export const formatSpan = (count, unit, locale) => new Intl.NumberFormat(locale, { style: "unit", unit, unitDisplay: "long" }).format(count);
