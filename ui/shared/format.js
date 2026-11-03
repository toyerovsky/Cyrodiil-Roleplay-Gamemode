import { fill } from "./locale.js";

const SECONDS_IN_MINUTE = 60;
const MINUTES_IN_HOUR = 60;

export const formatDate = (milliseconds, locale) => new Intl.DateTimeFormat(locale, { dateStyle: "long" }).format(milliseconds);

export const formatMoment = (milliseconds, locale) =>
  new Intl.DateTimeFormat(locale, { dateStyle: "long", timeStyle: "short" }).format(milliseconds);

export function formatPlayed(seconds, t, locale) {
  const minutes = Math.floor(seconds / SECONDS_IN_MINUTE);
  const hours = Math.floor(minutes / MINUTES_IN_HOUR);
  return hours > 0
    ? fill(t.hours_minutes, { hours: hours.toLocaleString(locale), minutes: minutes % MINUTES_IN_HOUR })
    : fill(t.minutes, { minutes });
}
