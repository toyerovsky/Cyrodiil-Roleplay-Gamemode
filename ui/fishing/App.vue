<script setup>
import { computed, onMounted, onUnmounted, ref } from "vue";
import { fill, usePageTexts } from "../shared/locale.js";

// The fishing's marks over the game, which never take the input: what the line asks for now, a ring over the float at
// the bite and while landing, the rod's bend against the safe band, and the catch. The server keeps every time and the
// game reads the keys; the page only shows.
const props = defineProps({ bridge: { type: Object, required: true } });

const NIBBLE_SHOWN_MS = 800;
const CARD_SHOWN_MS = 3600;
const PERCENT = 100;
const LOST = Object.freeze({
  LostEarly: "lost_early",
  LostNibble: "lost_nibble",
  LostLate: "lost_late",
  LostSnapped: "lost_snapped",
  LostSlack: "lost_slack",
});
const CATCHES = Object.freeze({ Caught: "caught", Found: "found" });
const HINTS = Object.freeze({ Aim: "aim_hint", Waiting: "waiting_hint", Bite: "bite_hint", Landing: "landing_hint" });
// The stages the space key does something in.
const KEYED = new Set(["Aim", "Bite", "Landing"]);
const MARKED = new Set(["Aim", "Bite", "Landing"]);

const { locale, t } = usePageTexts(props.bridge, "fishing");
const view = ref(null);
const anchor = ref(null);
const nibbling = ref(false);
const card = ref(null);
const timers = {};

const stage = computed(() => view.value?.stage ?? null);
const landing = computed(() => (stage.value === "Landing" ? view.value : null));
const outcome = computed(() => view.value?.outcome ?? null);

// The rod's bend against the safe band: "safe", "tight" or "slack".
const line = computed(() => {
  if (!landing.value) return "";
  if (landing.value.bend > landing.value.safeTo) return "tight";
  if (landing.value.bend < landing.value.safeFrom) return "slack";
  return "safe";
});

const title = computed(() => {
  switch (stage.value) {
    case "Aim":
      return t.value.aim;
    case "Waiting":
      return nibbling.value ? t.value.nibble : t.value.waiting;
    case "Bite":
      return t.value.bite;
    case "Landing":
      return line.value === "safe" ? t.value.landing : t.value[line.value];
    case "Rest":
      return outcome.value ? t.value[LOST[outcome.value.kind] ?? CATCHES[outcome.value.kind]] : "";
    default:
      return "";
  }
});
const lost = computed(() => stage.value === "Rest" && outcome.value && outcome.value.kind in LOST);
const hint = computed(() => (HINTS[stage.value] ? t.value[HINTS[stage.value]] : ""));
const keyed = computed(() => KEYED.has(stage.value));
const marked = computed(() => !!anchor.value && MARKED.has(stage.value));
const markStyle = computed(() => ({ left: percent(anchor.value.x), top: percent(anchor.value.y) }));

function percent(share) {
  return `${Math.round(share * PERCENT * 10) / 10}%`;
}

function later(name, ms, run) {
  clearTimeout(timers[name]);
  timers[name] = setTimeout(run, ms);
}

function onState(json) {
  const next = JSON.parse(json);
  const before = view.value;
  view.value = next;
  if (before && next.nibbles > before.nibbles) {
    nibbling.value = true;
    later("nibble", NIBBLE_SHOWN_MS, () => (nibbling.value = false));
  }
  const ended = next.outcome;
  if (ended && ended.number !== before?.outcome?.number && ended.kind in CATCHES) {
    card.value = ended;
    later("card", CARD_SHOWN_MS, () => (card.value = null));
  }
}

function onAnchor(x, y, shown) {
  anchor.value = shown ? { x, y } : null;
}

function onEnded() {
  view.value = null;
  anchor.value = null;
  card.value = null;
  nibbling.value = false;
}

onMounted(() => {
  props.bridge.onState(onState);
  props.bridge.onAnchor(onAnchor);
  props.bridge.onEnded(onEnded);
  props.bridge.signalReady();
});

onUnmounted(() => Object.values(timers).forEach(clearTimeout));
</script>

<template>
  <div v-if="view" class="fishing" :lang="locale" :data-stage="stage">
    <div v-if="marked" class="fishing__mark" :class="`fishing__mark--${stage.toLowerCase()}`" :style="markStyle" aria-hidden="true">
      <span :key="view.bites" class="fishing__ring" :style="{ animationDuration: `${view.strikeWindowMs}ms` }"></span>
    </div>

    <Transition name="fishing-card">
      <section v-if="card" :key="card.number" class="fishing__card rp-plate" role="status" data-card>
        <p class="fishing__card-kind">{{ t[CATCHES[card.kind]] }}</p>
        <h2 class="fishing__card-name">{{ card.name }}</h2>
        <p v-if="card.kind === 'Caught'" class="fishing__card-size">
          <span>{{ fill(t.length, { length: card.lengthCm }) }}</span>
          <span>{{ fill(t.weight, { grams: card.grams }) }}</span>
        </p>
        <p v-if="card.trophy" class="fishing__trophy">{{ t.trophy }}</p>
      </section>
    </Transition>

    <section class="fishing__plate rp-plate" :class="{ [`fishing__plate--${line}`]: !!line, 'fishing__plate--lost': lost }">
      <p class="fishing__title" role="status">{{ title }}</p>
      <template v-if="landing">
        <div
          class="fishing__meter"
          role="meter"
          :aria-label="t.bend"
          aria-valuemin="0"
          aria-valuemax="1"
          :aria-valuenow="landing.bend"
        >
          <span class="fishing__band" :style="{ left: percent(landing.safeFrom), width: percent(landing.safeTo - landing.safeFrom) }"></span>
          <span class="fishing__needle" :style="{ left: percent(landing.bend) }"></span>
        </div>
        <div class="fishing__tired" aria-hidden="true"><span :style="{ width: percent(landing.tired) }"></span></div>
      </template>
      <p v-if="hint" class="fishing__hint">
        <kbd v-if="keyed" class="fishing__key">{{ t.key_space }}</kbd>
        <span>{{ hint }}</span>
      </p>
      <p class="fishing__stop">
        <kbd class="fishing__key fishing__key--quiet">{{ t.key_move }}</kbd>
        <span>{{ t.stop_hint }}</span>
      </p>
    </section>
  </div>
</template>
