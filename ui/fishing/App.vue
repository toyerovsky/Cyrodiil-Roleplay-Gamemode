<script setup>
import { computed, onMounted, onUnmounted, ref } from "vue";
import { fill, usePageTexts } from "../shared/locale.js";
import CatchCard from "./CatchCard.vue";
import { findPower, NEAREST_CAST_CM } from "./charge.js";
import FightGauges from "./FightGauges.vue";
import GearCorner from "./GearCorner.vue";
import { isCatch, OUTCOME_TEXTS } from "./outcomes.js";
import PullArrow from "./PullArrow.vue";

// Shown over the game, never taking the input: the server keeps every time and the game reads the keys.
const props = defineProps({ bridge: { type: Object, required: true } });

const NIBBLE_SHOWN_MS = 800;
const CARD_SHOWN_MS = 6000;
const PERCENT = 100;
const MARKED = new Set(["Aim", "Waiting", "Bite", "Fight"]);

const { locale, t } = usePageTexts(props.bridge, "fishing");
const view = ref(null);
const anchor = ref(null);
const nibbling = ref(false);
const card = ref(null);
const charging = ref(false);
const power = ref(0);
const timers = {};
let frame = 0;
let chargedFrom = 0;
let chargeUpMs = 0;

const stage = computed(() => view.value?.stage ?? null);
const fight = computed(() => (stage.value === "Fight" ? view.value.fight : null));
const outcome = computed(() => (stage.value === "Rest" ? view.value.outcome : null));
const lost = computed(() => !!outcome.value && !isCatch(outcome.value.kind));

const title = computed(() => {
  switch (stage.value) {
    case "Aim":
      return t.value.aim;
    case "Waiting":
      return nibbling.value ? t.value.nibble : t.value.waiting;
    case "Bite":
      return t.value.bite;
    case "Fight":
      return t.value[`pull.${fight.value.pull}`];
    case "Rest":
      return outcome.value ? t.value[OUTCOME_TEXTS[outcome.value.kind]] : "";
    default:
      return "";
  }
});

// Whether the player does what the fish's pull asks for: reels while it rests, leans against a run, eases off a dive.
const countered = computed(() => {
  const line = fight.value;
  if (!line) return false;
  switch (line.pull) {
    case "Rest":
      return line.reeling;
    case "Left":
      return line.lean === 1;
    case "Right":
      return line.lean === -1;
    case "Away":
      return !line.reeling;
    default:
      return false;
  }
});

const hint = computed(() => {
  if (stage.value === "Waiting") return t.value.waiting_hint;
  if (fight.value) return t.value[`counter.${fight.value.pull}`];
  return "";
});

const keys = computed(() => {
  const stop = { id: "stop", cap: t.value.key_stop, label: t.value["keys.stop"] };
  switch (stage.value) {
    case "Aim":
      return [{ id: "cast", cap: t.value.key_space, label: t.value["keys.cast"], on: charging.value, wanted: !charging.value }, stop];
    case "Bite":
      return [{ id: "strike", cap: t.value.key_space, label: t.value["keys.strike"], wanted: true }, stop];
    case "Fight":
      return [...listFightKeys(fight.value), stop];
    default:
      return [stop];
  }
});

function listFightKeys(line) {
  const red = line.tension >= line.tensionWarn;
  return [
    {
      id: "reel",
      cap: t.value.key_space,
      label: t.value["keys.reel"],
      on: line.reeling,
      wanted: !line.reeling && (line.pull === "Rest" || line.slack > 0),
      strain: line.reeling && line.pull !== "Rest" && red,
    },
    {
      id: "left",
      cap: t.value.key_a,
      label: t.value["keys.lean_left"],
      on: line.lean === -1,
      wanted: line.pull === "Right" && line.lean !== -1,
      strain: line.lean === -1 && line.pull === "Left",
    },
    {
      id: "right",
      cap: t.value.key_d,
      label: t.value["keys.lean_right"],
      on: line.lean === 1,
      wanted: line.pull === "Left" && line.lean !== 1,
      strain: line.lean === 1 && line.pull === "Right",
    },
  ];
}

const marked = computed(() => !!anchor.value && MARKED.has(stage.value));
const markStyle = computed(() => ({ left: percent(anchor.value.x), top: percent(anchor.value.y) }));
const nearest = computed(() => fill(t.value.metres, { metres: formatMetres(NEAREST_CAST_CM) }));
const farthest = computed(() => fill(t.value.metres, { metres: formatMetres(view.value.rod.reachCm) }));

function percent(share) {
  return `${Math.round(share * PERCENT * 10) / 10}%`;
}

function formatMetres(cm) {
  return (cm / PERCENT).toLocaleString(locale.value, { maximumFractionDigits: 1 });
}

function later(name, ms, run) {
  clearTimeout(timers[name]);
  timers[name] = setTimeout(run, ms);
}

function swing() {
  power.value = findPower(performance.now() - chargedFrom, chargeUpMs);
  frame = requestAnimationFrame(swing);
}

function onCharge(held, upMs) {
  cancelAnimationFrame(frame);
  charging.value = held;
  if (!held) return;

  chargedFrom = performance.now();
  chargeUpMs = upMs;
  power.value = 0;
  frame = requestAnimationFrame(swing);
}

function onState(json) {
  const next = JSON.parse(json);
  const before = view.value;
  view.value = next;
  if (next.stage === "Aim" && before?.stage !== "Aim" && !charging.value) power.value = 0;
  if (before && next.nibbles > before.nibbles) {
    nibbling.value = true;
    later("nibble", NIBBLE_SHOWN_MS, () => (nibbling.value = false));
  }

  const ended = next.outcome;
  if (ended && ended.number !== before?.outcome?.number) {
    card.value = ended;
    later("card", CARD_SHOWN_MS, () => (card.value = null));
  }
}

function onAnchor(x, y, shown) {
  anchor.value = shown ? { x, y } : null;
}

function onEnded() {
  cancelAnimationFrame(frame);
  Object.values(timers).forEach(clearTimeout);
  view.value = null;
  anchor.value = null;
  card.value = null;
  nibbling.value = false;
  charging.value = false;
  power.value = 0;
}

onMounted(() => {
  props.bridge.onState(onState);
  props.bridge.onAnchor(onAnchor);
  props.bridge.onCharge(onCharge);
  props.bridge.onEnded(onEnded);
  props.bridge.signalReady();
});

onUnmounted(() => {
  cancelAnimationFrame(frame);
  Object.values(timers).forEach(clearTimeout);
});
</script>

<template>
  <div v-if="view" class="fishing" :lang="locale" :data-stage="stage">
    <div
      v-if="marked"
      class="fishing__mark"
      :class="[`fishing__mark--${stage.toLowerCase()}`, { 'fishing__mark--charging': charging && stage === 'Aim' }]"
      :style="markStyle"
      aria-hidden="true"
    >
      <span v-if="stage === 'Bite'" :key="view.bites" class="fishing__ring" :style="{ animationDuration: `${view.strikeWindowMs}ms` }"></span>
      <span v-else class="fishing__ring"></span>
      <span v-if="stage === 'Waiting' && view.nibbles > 0" :key="view.nibbles" class="fishing__ripple"></span>
      <PullArrow v-if="fight && fight.pull !== 'Rest'" class="fishing__run" :class="`fishing__run--${fight.pull.toLowerCase()}`" :pull="fight.pull" />
    </div>

    <Transition name="fishing-card">
      <CatchCard v-if="card" :key="card.number" :card="card" :t="t" :locale="locale" />
    </Transition>

    <GearCorner :view="view" :t="t" :locale="locale" />

    <section class="fishing__plate rp-plate" :class="{ 'fishing__plate--lost': lost, 'fishing__plate--fight': !!fight }" :aria-label="t.title">
      <p class="fishing__title" role="status">
        <PullArrow v-if="fight" class="fishing__pull" :pull="fight.pull" />
        <span>{{ title }}</span>
      </p>
      <p v-if="hint" class="fishing__hint" :class="{ 'fishing__hint--done': countered }">{{ hint }}</p>

      <div v-if="stage === 'Aim'" class="fishing__power">
        <span class="fishing__power-end">{{ nearest }}</span>
        <div
          class="fishing__power-track"
          role="meter"
          :aria-label="t.power"
          aria-valuemin="0"
          aria-valuemax="1"
          :aria-valuenow="Math.round(power * PERCENT) / PERCENT"
          :class="{ 'fishing__power-track--charging': charging }"
        >
          <span class="fishing__power-fill" :style="{ transform: `scaleX(${power})` }"></span>
          <span class="fishing__power-tick" style="left: 25%"></span>
          <span class="fishing__power-tick" style="left: 50%"></span>
          <span class="fishing__power-tick" style="left: 75%"></span>
        </div>
        <span class="fishing__power-end">{{ farthest }}</span>
      </div>

      <FightGauges v-if="fight" :fight="fight" :t="t" :locale="locale" />

      <ul class="fishing__keys">
        <li
          v-for="key in keys"
          :key="key.id"
          class="fishing__key"
          :class="{ 'fishing__key--on': key.on, 'fishing__key--wanted': key.wanted, 'fishing__key--strain': key.strain }"
          :data-key="key.id"
        >
          <kbd>{{ key.cap }}</kbd>
          <span>{{ key.label }}</span>
        </li>
      </ul>
    </section>
  </div>
</template>
