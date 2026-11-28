<script setup>
import { computed } from "vue";

// The line's tension as a gauge round the float, where the eyes are in a fight: a ring open at the bottom that fills
// clockwise from the lower left, gold up to where the line is close to snapping and red past it, over a track whose
// last part is red from a tick on.
const props = defineProps({
  tension: { type: Number, required: true },
  warn: { type: Number, required: true },
});

const RADIUS = 42;
const TURN = 2 * Math.PI * RADIUS;
const DEGREES = 360;
// The ring's opening, and where the gauge begins: clockwise from three o'clock, as a circle is drawn.
const OPEN_DEGREES = 70;
const BEGINS_DEGREES = 90 + OPEN_DEGREES / 2;
const SWEEP = 1 - OPEN_DEGREES / DEGREES;
// The dark edge under the track runs a little past both its ends.
const EDGE_PAST = 2.5;

const clamp = (value) => Math.min(1, Math.max(0, value));
const round = (value) => Math.round(value * 10) / 10;
const along = (share) => round(clamp(share) * SWEEP * TURN);
const dash = (length, from = 0) => ({ "stroke-dasharray": `${round(length)} ${round(TURN)}`, "stroke-dashoffset": round(-from) || 0 });

const red = computed(() => props.tension >= props.warn);
const edge = dash(along(1) + 2 * EDGE_PAST, -EDGE_PAST);
const track = dash(along(1));
const danger = computed(() => dash(along(1) - along(props.warn), along(props.warn)));
const filled = computed(() => dash(along(props.tension)));
const tick = computed(() => `rotate(${round(BEGINS_DEGREES + 90 + clamp(props.warn) * SWEEP * DEGREES)} 50 50)`);
</script>

<template>
  <svg class="fishing-tension" :class="{ 'fishing-tension--red': red }" viewBox="0 0 100 100" data-ring="tension">
    <g :transform="`rotate(${BEGINS_DEGREES} 50 50)`">
      <circle class="fishing-tension__edge" cx="50" cy="50" :r="RADIUS" v-bind="edge" />
      <circle class="fishing-tension__track" cx="50" cy="50" :r="RADIUS" v-bind="track" />
      <circle class="fishing-tension__danger" cx="50" cy="50" :r="RADIUS" v-bind="danger" />
      <circle class="fishing-tension__fill" cx="50" cy="50" :r="RADIUS" v-bind="filled" data-fill />
    </g>
    <g :transform="tick" data-tick>
      <line class="fishing-tension__tick-edge" x1="50" y1="0.5" x2="50" y2="15.5" />
      <line class="fishing-tension__tick" x1="50" y1="1.5" x2="50" y2="14.5" />
    </g>
  </svg>
</template>
