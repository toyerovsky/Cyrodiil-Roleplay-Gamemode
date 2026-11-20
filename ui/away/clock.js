const SECONDS_IN_MINUTE = 60;
const SECONDS_IN_HOUR = 3600;
const DIGITS = 2;

const pad = (number) => String(number).padStart(DIGITS, "0");

// 4:05 under an hour, 1:02:03 from it.
export function formatClock(seconds) {
  const whole = Math.max(0, Math.floor(seconds));
  const hours = Math.floor(whole / SECONDS_IN_HOUR);
  const minutes = Math.floor((whole % SECONDS_IN_HOUR) / SECONDS_IN_MINUTE);
  const rest = whole % SECONDS_IN_MINUTE;
  return hours > 0 ? `${hours}:${pad(minutes)}:${pad(rest)}` : `${minutes}:${pad(rest)}`;
}
