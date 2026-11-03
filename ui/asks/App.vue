<script setup>
import { computed, onMounted, onUnmounted, ref } from "vue";
import { RpIcon } from "../rp/index.js";
import { fill, usePageTexts } from "../shared/locale.js";
import { ACCEPT_KEY, DECLINE_KEY, KINDS } from "./kinds.js";

const props = defineProps({ bridge: { type: Object, required: true } });

const TICK_MS = 250;
const MS_PER_SECOND = 1000;

const { t } = usePageTexts(props.bridge, "asks");

const asks = ref([]);
const answerable = ref(0);
const now = ref(Date.now());

// When each ask runs out, by id, set once: a state sent again for another ask does not move it.
const endsAt = new Map();
let ticker = 0;

const shown = computed(() =>
  asks.value.map((ask) => {
    const left = Math.max(0, (endsAt.get(ask.id) ?? 0) - now.value);
    return { ...ask, seconds: Math.ceil(left / MS_PER_SECOND), share: ask.totalMs > 0 ? left / ask.totalMs : 0 };
  }),
);

function onState(view) {
  const at = Date.now();
  for (const ask of view.asks) {
    if (!endsAt.has(ask.id)) endsAt.set(ask.id, at + ask.leftMs);
  }
  for (const id of [...endsAt.keys()]) {
    if (!view.asks.some((ask) => ask.id === id)) endsAt.delete(id);
  }

  asks.value = view.asks;
  answerable.value = view.answerable;
  now.value = at;
  if (view.asks.length && !ticker) ticker = setInterval(() => (now.value = Date.now()), TICK_MS);
  if (!view.asks.length) stopTicking();
}

function stopTicking() {
  clearInterval(ticker);
  ticker = 0;
}

onMounted(() => {
  props.bridge.onState(onState);
  props.bridge.signalReady();
});

onUnmounted(stopTicking);
</script>

<template>
  <section v-if="shown.length" class="asks" :aria-label="t.label" aria-live="polite">
    <article
      v-for="ask in shown"
      :key="ask.id"
      class="ask"
      :class="{ 'ask--incoming': ask.incoming, 'ask--answerable': ask.id === answerable }"
      :data-ask="ask.id"
    >
      <RpIcon class="ask__icon" :name="KINDS[ask.kind].icon" />
      <div class="ask__body">
        <p class="ask__title">{{ t[`title.${ask.kind}`] }}</p>
        <p class="ask__text">{{ fill(t[`${ask.incoming ? "incoming" : "outgoing"}.${ask.kind}`], { name: ask.name }) }}</p>
        <div v-if="ask.id === answerable" class="ask__keys">
          <span class="ask__key"><kbd class="ask__keycap">{{ ACCEPT_KEY }}</kbd>{{ t[`accept.${ask.kind}`] }}</span>
          <span class="ask__key"><kbd class="ask__keycap">{{ DECLINE_KEY }}</kbd>{{ t.decline }}</span>
        </div>
        <p v-else-if="ask.incoming" class="ask__next">{{ t.next }}</p>
      </div>
      <p class="ask__seconds">{{ fill(t.seconds, { seconds: ask.seconds }) }}</p>
      <div class="ask__time" aria-hidden="true"><div class="ask__time-left" :style="{ transform: `scaleX(${ask.share})` }"></div></div>
    </article>
  </section>
</template>
