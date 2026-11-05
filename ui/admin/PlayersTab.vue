<script setup>
import { computed, ref } from "vue";
import { RpChip, RpField } from "../rp/index.js";
import { fill } from "../shared/locale.js";
import PlayerCard from "./PlayerCard.vue";

const props = defineProps({
  players: { type: Array, required: true },
  selected: { type: Number, default: null },
  card: { type: Object, default: null },
  lore: { type: Object, default: null },
  loreAsked: Boolean,
  found: { type: Array, required: true },
  t: { type: Object, required: true },
  locale: { type: String, required: true },
  may: { type: Function, required: true },
  can: { type: Function, required: true },
});

const emit = defineEmits(["select", "act", "search", "lore"]);

const FILTERS = {
  all: () => true,
  staff: (player) => player.roles.length > 0,
  muted: (player) => player.muted,
  frozen: (player) => player.frozen,
};

const query = ref("");
const filter = ref("all");

const shown = computed(() => {
  const text = query.value.trim().toLocaleLowerCase(props.locale);
  return props.players.filter(
    (player) =>
      FILTERS[filter.value](player) &&
      (text === "" ||
        String(player.number) === text ||
        player.name.toLocaleLowerCase(props.locale).includes(text) ||
        player.nickname.toLocaleLowerCase(props.locale).includes(text)),
  );
});
const picked = computed(() => props.players.find((player) => player.number === props.selected) ?? null);

const describeStatus = (player) => {
  if (!player.character) return props.t["player.no_character"];
  if (player.area === null) return props.t["player.away"];
  return fill(props.t["player.health"], { health: player.health, max: player.healthMax });
};
</script>

<template>
  <div class="admin-players">
    <section class="admin-players__list" :aria-label="t['players.list']">
      <RpField v-model="query" class="admin-players__search" :label="t['players.search']" type="search" />
      <!-- Toggle buttons, as the trader's kinds of wares: each one a stop of Tab, picked with Enter or Space. -->
      <div class="admin-chips" role="group" :aria-label="t['players.filter']">
        <RpChip v-for="(_, id) in FILTERS" :key="id" :pressed="filter === id" @click="filter = id">
          {{ t[`filter.${id}`] }}
        </RpChip>
      </div>
      <ul v-if="shown.length" class="admin-players__rows">
        <li v-for="player in shown" :key="player.number">
          <button
            type="button"
            class="admin-player"
            :class="{ 'admin-player--on': player.number === selected }"
            :aria-pressed="player.number === selected"
            :data-player="player.number"
            @click="emit('select', player.number)"
          >
            <span class="admin-player__number">[{{ player.number }}]</span>
            <span class="admin-player__name">{{ player.name }}</span>
            <span class="admin-player__nickname">{{ player.nickname }}</span>
            <span class="admin-player__where">{{ describeStatus(player) }}</span>
            <span class="admin-player__marks">
              <span v-if="player.onDuty" class="admin-mark admin-mark--duty">{{ t["player.on_duty"] }}</span>
              <span v-if="player.muted" class="admin-mark">{{ t["kind.admin.mute"] }}</span>
              <span v-if="player.frozen" class="admin-mark">{{ t["kind.admin.freeze"] }}</span>
            </span>
          </button>
        </li>
      </ul>
      <p v-else class="admin__note">{{ t["players.none"] }}</p>
    </section>

    <PlayerCard
      v-if="picked"
      :player="picked"
      :card="card"
      :lore="lore"
      :lore-asked="loreAsked"
      :found="found"
      :t="t"
      :locale="locale"
      :may="may"
      :can="can"
      @act="(action, values, name) => emit('act', action, values, name)"
      @search="(text) => emit('search', text)"
      @lore="emit('lore')"
    />
    <p v-else class="admin-players__pick admin__note">{{ t["card.pick"] }}</p>
  </div>
</template>
