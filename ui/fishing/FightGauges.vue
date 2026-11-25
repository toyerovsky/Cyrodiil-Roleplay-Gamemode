<script setup>
import { computed } from "vue";
import { fill } from "../shared/locale.js";

// The fight as the server last told it: the line's tension against where it snaps, the fish's strength, how far it is,
// how heavy it feels, which way the rod leans, and the slack that throws the hook.
const props = defineProps({
  fight: { type: Object, required: true },
  t: { type: Object, required: true },
  locale: { type: String, required: true },
});

const PERCENT = 100;
const CM_IN_METRE = 100;
const LEANS = Object.freeze({ "-1": "left", 0: "upright", 1: "right" });
// The run each lean goes with: leaning with a run strains the line, against it tires the fish.
const WITH_RUN = Object.freeze({ "-1": "Left", 1: "Right" });
const AGAINST_RUN = Object.freeze({ "-1": "Right", 1: "Left" });

const share = (value) => `${Math.round(Math.min(1, Math.max(0, value)) * PERCENT * 10) / 10}%`;
const whole = (value) => `${Math.round(Math.min(1, Math.max(0, value)) * PERCENT)}%`;

const red = computed(() => props.fight.tension >= props.fight.tensionWarn);
const distance = computed(() =>
  fill(props.t.distance, {
    metres: (props.fight.distanceCm / CM_IN_METRE).toLocaleString(props.locale, { minimumFractionDigits: 1, maximumFractionDigits: 1 }),
  }),
);
const lean = computed(() => {
  const way = props.fight.lean;
  if (WITH_RUN[way] === props.fight.pull) return "with";
  if (AGAINST_RUN[way] === props.fight.pull) return "against";
  return "still";
});
</script>

<template>
  <div class="fishing-fight" :data-feel="fight.feel">
    <div class="fishing-fight__gauge" :class="{ 'fishing-fight__gauge--red': red }" data-gauge="tension">
      <span class="fishing-fight__label">{{ t.tension }}</span>
      <div class="fishing-fight__track" role="meter" :aria-label="t.tension" aria-valuemin="0" aria-valuemax="1" :aria-valuenow="fight.tension">
        <span class="fishing-fight__danger" :style="{ left: share(fight.tensionWarn) }"></span>
        <span class="fishing-fight__fill" :style="{ width: share(fight.tension) }"></span>
        <span class="fishing-fight__needle" :style="{ left: share(fight.tension) }"></span>
      </div>
      <span class="fishing-fight__value">{{ whole(fight.tension) }}</span>
    </div>

    <div class="fishing-fight__gauge fishing-fight__gauge--stamina" data-gauge="stamina">
      <span class="fishing-fight__label">{{ t.stamina }}</span>
      <div class="fishing-fight__track" role="meter" :aria-label="t.stamina" aria-valuemin="0" aria-valuemax="1" :aria-valuenow="fight.stamina">
        <span class="fishing-fight__fill" :style="{ width: share(fight.stamina) }"></span>
      </div>
      <span class="fishing-fight__value">{{ whole(fight.stamina) }}</span>
    </div>

    <div class="fishing-fight__facts">
      <span class="fishing-fight__feel" :class="`fishing-fight__feel--${fight.feel.toLowerCase()}`">{{ t[`feel.${fight.feel}`] }}</span>
      <span class="fishing-fight__lean" :class="`fishing-fight__lean--${lean}`" data-lean>
        <svg class="fishing-fight__rod" :style="{ transform: `rotate(${fight.lean * 28}deg)` }" viewBox="0 0 24 24" aria-hidden="true">
          <path d="M12 22V4M12 4c2 1 3.5 3 4 6" />
        </svg>
        <span>{{ t[`lean.${LEANS[fight.lean]}`] }}</span>
      </span>
      <span class="fishing-fight__distance">
        <span>{{ distance }}</span>
        <span v-if="fight.reeling" class="fishing-fight__reeling">{{ t.reeling }}</span>
      </span>
    </div>

    <p v-if="fight.slack > 0" class="fishing-fight__slack" role="alert">
      <span>{{ t.slack }}</span>
      <span class="fishing-fight__slack-track" aria-hidden="true"><span :style="{ width: share(fight.slack) }"></span></span>
    </p>
  </div>
</template>
