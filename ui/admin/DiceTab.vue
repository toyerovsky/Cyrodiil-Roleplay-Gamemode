<script setup>
import { computed, ref } from "vue";
import { formatAsked, formatDice } from "../dice/notation.js";
import { RpButton, RpField } from "../rp/index.js";
import { fill, pickTexts } from "../shared/locale.js";
import { formatMoment } from "./format.js";

// The roll log, newest first, a page at a time as the audit. The dice's own words (d20 or k20) are the dice board's.
const props = defineProps({
  rolls: { type: Object, default: null },
  t: { type: Object, required: true },
  locale: { type: String, required: true },
});

const emit = defineEmits(["search"]);

const player = ref("");
const words = computed(() => pickTexts(props.locale, "ui.dice."));

const search = () => emit("search", { player: player.value.trim() || null });
const loadOlder = () => emit("search", null, props.rolls.rolls.at(-1).id);

const rows = computed(() =>
  (props.rolls?.rolls ?? []).map((roll) => ({
    ...roll,
    label: [formatAsked(roll.dice, words.value), roll.reason && fill(props.t["dice.for"], { reason: roll.reason })].filter(Boolean).join(" "),
    list: roll.dice.length > 1 ? formatDice(roll.dice, words.value) : "",
  })),
);
</script>

<template>
  <div class="admin-audit admin-dice">
    <form class="admin-dice__filter" @submit.prevent="search">
      <p class="admin__note">{{ t["dice.lead"] }}</p>
      <RpField v-model="player" :label="t['dice.player']" />
      <RpButton type="submit" variant="primary">{{ t["dice.search"] }}</RpButton>
    </form>

    <p v-if="!rolls" class="admin__note" role="status">{{ t.loading }}</p>
    <p v-else-if="!rows.length" class="admin__note">{{ t["dice.none"] }}</p>
    <template v-else>
      <ol class="admin-entries">
        <li v-for="roll in rows" :key="roll.id" class="admin-entry admin-roll">
          <span class="admin-entry__at">{{ formatMoment(roll.at, locale) }}</span>
          <span class="admin-entry__action">{{ roll.character }}</span>
          <span class="admin-roll__asked">
            <span>{{ roll.label }}</span>
            <b class="admin-roll__total">{{ roll.total }}</b>
          </span>
          <span v-if="roll.list || roll.area" class="admin-entry__details">{{ [roll.list, roll.area].filter(Boolean).join(" · ") }}</span>
        </li>
      </ol>
      <RpButton v-if="rolls.more" class="admin-audit__older" @click="loadOlder">{{ t["dice.older"] }}</RpButton>
    </template>
  </div>
</template>
