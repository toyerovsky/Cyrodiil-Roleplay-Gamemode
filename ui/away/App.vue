<script setup>
import { computed, onMounted, onUnmounted, ref } from "vue";
import { usePageTexts } from "../shared/locale.js";
import { formatClock } from "./clock.js";

// The AFK screen over the whole game, which takes no keys: first how long until [AFK] shows over the player's head, then
// how long they have been away. The game sends the second it counts from, the page counts on by itself, and the screen
// goes the moment the player is back.
const props = defineProps({ bridge: { type: Object, required: true } });

// More often than once a second, so a late timer never skips a second on the clock.
const TICK_MS = 250;
const MS_PER_SECOND = 1000;
const WARNING = "warning";
const AWAY = "away";
// The hourglass running out, then the moon of a rest.
const ICONS = {
  [WARNING]: "M6 3h12M6 21h12M7.5 3c0 5 4.5 6 4.5 9s-4.5 4-4.5 9M16.5 3c0 5-4.5 6-4.5 9s4.5 4 4.5 9M9.5 19h5",
  [AWAY]: "M19.5 14.8A8 8 0 0 1 9.2 4.5a8 8 0 1 0 10.3 10.3z",
};

const { locale, t } = usePageTexts(props.bridge, "away");

const shown = ref(null);
const now = ref(Date.now());
let ticker = 0;

const seconds = computed(() => {
  if (!shown.value) return 0;
  const passed = Math.floor((now.value - shown.value.at) / MS_PER_SECOND);
  return shown.value.kind === WARNING ? Math.max(0, shown.value.seconds - passed) : shown.value.seconds + passed;
});
const clock = computed(() => formatClock(seconds.value));

function show(kind, value) {
  const at = Date.now();
  shown.value = { kind, seconds: Math.max(0, Math.floor(Number(value) || 0)), at };
  now.value = at;
  clearInterval(ticker);
  ticker = setInterval(() => (now.value = Date.now()), TICK_MS);
}

function end() {
  shown.value = null;
  clearInterval(ticker);
}

onMounted(() => {
  props.bridge.onWarning((value) => show(WARNING, value));
  props.bridge.onMarked((value) => show(AWAY, value));
  props.bridge.onEnded(end);
  props.bridge.signalReady();
});

onUnmounted(() => clearInterval(ticker));
</script>

<template>
  <div v-if="shown" class="away" :class="`away--${shown.kind}`" :lang="locale" role="status" :data-kind="shown.kind">
    <div class="away__veil" aria-hidden="true"></div>
    <header class="away__head">
      <div class="away__crest" aria-hidden="true">
        <span class="away__rule"></span>
        <svg class="away__icon" viewBox="0 0 24 24"><path :d="ICONS[shown.kind]" /></svg>
        <span class="away__rule away__rule--right"></span>
      </div>
      <h1 class="away__title">{{ t[`${shown.kind}_title`] }}</h1>
      <p class="away__clock">
        <span class="away__time">{{ clock }}</span>
        <span class="away__label">{{ t[`${shown.kind}_clock`] }}</span>
      </p>
    </header>
    <footer class="away__foot">
      <p v-if="shown.kind === AWAY" class="away__seen">{{ t.away_seen }}</p>
      <p class="away__hint">{{ t[`${shown.kind}_hint`] }}</p>
    </footer>
  </div>
</template>
