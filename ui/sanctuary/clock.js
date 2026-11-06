import { onUnmounted, ref } from "vue";

const SECOND_MS = 1000;
const SECONDS_IN_MINUTE = 60;

// What is left of an aggressor's streak as a clock: 4:05.
export function formatLeft(seconds) {
  const whole = Math.max(0, Math.ceil(seconds));
  return `${Math.floor(whole / SECONDS_IN_MINUTE)}:${String(whole % SECONDS_IN_MINUTE).padStart(2, "0")}`;
}

// Seconds that count down from the last number the server sent, a tick a second, never below 0.
export function useCountdown() {
  const left = ref(0);
  let timer = null;

  function stop() {
    clearInterval(timer);
    timer = null;
  }

  function start(seconds) {
    stop();
    left.value = Math.max(0, seconds);
    if (left.value === 0) return;
    const ends = Date.now() + left.value * SECOND_MS;
    timer = setInterval(() => {
      left.value = Math.max(0, Math.ceil((ends - Date.now()) / SECOND_MS));
      if (left.value === 0) stop();
    }, SECOND_MS);
  }

  onUnmounted(stop);
  return { left, start, stop };
}
