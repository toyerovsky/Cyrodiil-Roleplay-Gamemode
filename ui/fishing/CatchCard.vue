<script setup>
import { computed } from "vue";
import { RpIcon, RpPanel } from "../rp/index.js";
import { findSpeciesPicture } from "../shared/fishing/pictures.js";
import { fill } from "../shared/locale.js";
import { isCatch, OUTCOME_TEXTS } from "./outcomes.js";

// What came of a bite, on a card of the game's paper: the fish as painted, its size and what it earned, or what got
// away, and what either cost the gear.
const props = defineProps({
  card: { type: Object, required: true },
  t: { type: Object, required: true },
  locale: { type: String, required: true },
});

const GRAMS_IN_KILOGRAM = 1000;

const caught = computed(() => isCatch(props.card.kind));
const picture = computed(() => findSpeciesPicture(props.card.species));
const eyebrow = computed(() => props.t[caught.value ? `card.${OUTCOME_TEXTS[props.card.kind]}` : "card.lost"]);
const heading = computed(() => (caught.value ? props.card.name : props.t[OUTCOME_TEXTS[props.card.kind]]));
const sized = computed(() => props.card.kind === "Caught" && props.card.lengthCm > 0);

const length = computed(() => fill(props.t.length, { length: props.card.lengthCm.toLocaleString(props.locale) }));
const weight = computed(() => {
  const grams = props.card.grams;
  if (grams < GRAMS_IN_KILOGRAM) return fill(props.t.weight_grams, { grams: grams.toLocaleString(props.locale) });
  const kilograms = (grams / GRAMS_IN_KILOGRAM).toLocaleString(props.locale, { maximumFractionDigits: 1 });
  return fill(props.t.weight_kilograms, { kilograms });
});
</script>

<template>
  <RpPanel
    class="fishing-card"
    :class="{ 'fishing-card--lost': !caught, 'fishing-card--rare': card.rare }"
    :title="heading"
    role="status"
    data-card
    :data-kind="card.kind"
  >
    <template #title>
      <span class="fishing-card__eyebrow">{{ eyebrow }}</span>
      <span class="fishing-card__name">{{ heading }}</span>
    </template>

    <div v-if="picture" class="fishing-card__picture">
      <img :src="picture" alt="" />
    </div>
    <div v-else-if="caught" class="fishing-card__picture fishing-card__picture--find">
      <RpIcon name="bait" />
    </div>
    <p v-if="!caught && card.name" class="fishing-card__lost-name">{{ card.name }}</p>

    <p v-if="sized" class="fishing-card__size">
      <span data-length>{{ length }}</span>
      <span aria-hidden="true">·</span>
      <span data-weight>{{ weight }}</span>
    </p>

    <ul v-if="card.trophy || card.record || card.rare" class="fishing-card__badges">
      <li v-if="card.trophy" class="fishing-card__badge" data-badge="trophy">{{ t.trophy }}</li>
      <li v-if="card.record" class="fishing-card__badge fishing-card__badge--record" data-badge="record">{{ t.record }}</li>
      <li v-if="card.rare" class="fishing-card__badge fishing-card__badge--rare" data-badge="rare">{{ t.rare }}</li>
    </ul>

    <p v-if="card.xp > 0" class="fishing-card__xp" data-xp>{{ fill(t.xp_gained, { xp: card.xp.toLocaleString(locale) }) }}</p>
    <p v-if="card.levelUp" class="fishing-card__level" data-level>{{ fill(t.level_reached, { level: card.levelUp }) }}</p>
    <p v-if="card.rod" class="fishing-card__cost" :class="{ 'fishing-card__cost--broken': card.rod === 'Broken' }" data-rod>
      {{ t[`rod_now.${card.rod}`] }}
    </p>
    <p v-if="card.baitLost" class="fishing-card__cost" data-bait>{{ t.lure_lost }}</p>
  </RpPanel>
</template>
