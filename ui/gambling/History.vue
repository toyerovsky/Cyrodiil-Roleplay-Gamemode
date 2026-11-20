<script setup>
import { computed } from "vue";
import { fill, pickTexts } from "../shared/locale.js";
import { formatMoment } from "../shared/format.js";
const props = defineProps({ games: { type: Array, required: true }, staff: Boolean, locale: { type: String, required: true } });
const t = computed(() => pickTexts(props.locale, "ui.gambling."));
</script>

<template>
  <section class="menu__group">
    <h2 class="menu__heading">{{ t.history }}</h2>
    <p v-if="!games.length">{{ t.empty_history }}</p>
    <ol v-else class="gambling-history">
      <li v-for="game in games" :key="game.id">
        <b>{{ t[game.mode] }} · {{ staff ? `${game.first} / ${game.second}` : game.opponent }}</b>
        <p>{{ fill(t.history_row, { stake: game.stake, pot: game.pot }) }}</p>
        <p>{{ fill(t[game.status], { winner: game.winner }) }}</p>
        <small>{{ formatMoment(game.at, locale) }}</small>
        <slot :game="game" />
      </li>
    </ol>
  </section>
</template>

<style>
.gambling-history { display: flex; flex-direction: column; gap: 16px; list-style: none; padding: 0; }
.gambling-history li { padding-bottom: 14px; border-bottom: 1px solid var(--ink-line); overflow-wrap: anywhere; }
.gambling-history p { margin: 4px 0; }
</style>
