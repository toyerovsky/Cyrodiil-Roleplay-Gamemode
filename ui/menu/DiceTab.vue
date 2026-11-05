<script setup>
import { computed } from "vue";
import { iconOf } from "../dice/icons.js";
import { formatAsked, formatDice } from "../dice/notation.js";
import { RpButton, RpChip } from "../rp/index.js";
import { fill, pickTexts } from "../shared/locale.js";
import { formatMoment } from "../shared/format.js";

// The dice's own words (the sets' names, d20 or k20) are the dice board's.
const props = defineProps({
  dice: { type: Object, required: true },
  t: { type: Object, required: true },
  locale: { type: String, required: true },
});

const emit = defineEmits(["open", "pick"]);

const words = computed(() => pickTexts(props.locale, "ui.dice."));

const RECENT_SHOWN = 10;
const recent = computed(() =>
  props.dice.recent.slice(0, RECENT_SHOWN).map((roll) => ({
    key: `${roll.at}-${roll.total}-${roll.dice.length}`,
    asked: formatAsked(roll.dice, words.value),
    dice: roll.dice.length > 1 ? formatDice(roll.dice, words.value) : "",
    total: roll.total,
    reason: roll.reason,
    at: formatMoment(roll.at, props.locale),
  })),
);
</script>

<template>
  <p class="menu__note">{{ t["dice.lead"] }}</p>
  <RpButton class="menu__dice-open" variant="primary" @click="emit('open')">{{ t["dice.open"] }}</RpButton>

  <section class="menu__group">
    <h2 class="menu__heading">{{ t["dice.set"] }}</h2>
    <div class="menu__dice-sets" role="group" :aria-label="t['dice.set']">
      <RpChip v-for="id in dice.sets" :key="id" class="menu__dice-set" :pressed="id === dice.set" :data-set="id" @click="emit('pick', id)">
        <img class="menu__dice-icon" :src="iconOf(id, 'd20')" alt="">
        <span>{{ words[`set.${id}`] }}</span>
      </RpChip>
    </div>
    <p class="menu__dice-text">{{ words[`set_text.${dice.set}`] }}</p>
  </section>

  <section class="menu__group">
    <h2 class="menu__heading">{{ t["dice.recent"] }}</h2>
    <p v-if="!recent.length" class="menu__note">{{ t["dice.none"] }}</p>
    <ol v-else class="menu__rolls">
      <li v-for="roll in recent" :key="roll.key" class="menu__roll">
        <span class="menu__roll-asked">
          <b>{{ roll.asked }}</b>
          <span v-if="roll.reason">{{ fill(t["dice.for"], { reason: roll.reason }) }}</span>
          <small>{{ roll.at }}</small>
        </span>
        <b class="menu__roll-total">{{ roll.total }}</b>
        <span v-if="roll.dice" class="menu__roll-dice">{{ roll.dice }}</span>
      </li>
    </ol>
  </section>
</template>
