<script setup>
import { computed, onUnmounted, ref, useId, watch } from "vue";
import { RpButton, RpChip, RpField } from "../rp/index.js";
import { MOST_ITEM_PIECES } from "../shared/limits.js";
import { fill } from "../shared/locale.js";
import { ITEM_TYPES, QUICK_COUNTS, SEARCH_AFTER_MS, SEARCH_FROM } from "./items.js";

// The game's items for the staff member themself or a player in the world: a type, a few letters, how many, Give.
const props = defineProps({
  view: { type: Object, required: true },
  found: { type: Array, required: true },
  recent: { type: Array, required: true },
  t: { type: Object, required: true },
  can: { type: Function, required: true },
});

const emit = defineEmits(["search", "act"]);

const ALL = "";
const WHOLE = /^\d+$/;

const text = ref("");
const type = ref(ALL);
const count = ref("1");
const target = ref(props.view.self);
const problem = ref("");
const countId = useId();
const problemId = useId();

let searchTimer = 0;

const searching = computed(() => text.value.trim().length >= SEARCH_FROM || type.value !== ALL);
// Those who can carry an item: the staff member first, then everyone in the world.
const targets = computed(() => {
  const inWorld = props.view.players.filter((player) => player.character && player.area !== null);
  const self = inWorld.filter((player) => player.number === props.view.self);
  return [...self, ...inWorld.filter((player) => player.number !== props.view.self)];
});
const targetName = computed(() => props.view.players.find((player) => player.number === target.value)?.name ?? "");
const describeTarget = (player) =>
  player.number === props.view.self ? fill(props.t["items.for_self"], { name: player.name }) : `[${player.number}] ${player.name}`;

watch([text, type], () => {
  clearTimeout(searchTimer);
  searchTimer = setTimeout(() => emit("search", searching.value ? { text: text.value.trim(), type: type.value } : null), SEARCH_AFTER_MS);
});

// The one picked may leave the world: the staff member comes back, or the first who is in it.
watch(
  targets,
  (now) => {
    if (!now.some((player) => player.number === target.value)) target.value = now[0]?.number ?? props.view.self;
  },
  { immediate: true },
);

function readCount() {
  const typed = count.value.trim();
  const value = WHOLE.test(typed) ? Number(typed) : NaN;
  return value >= 1 && value <= MOST_ITEM_PIECES ? value : null;
}

function give(entry) {
  const pieces = readCount();
  if (pieces === null) {
    problem.value = fill(props.t["dialog.count_range"], { max: MOST_ITEM_PIECES });
    return;
  }

  problem.value = "";
  emit("act", "giveNow", { number: target.value, item: entry.id, count: pieces, entry }, targetName.value);
}

function pickCount(value) {
  count.value = String(value);
  problem.value = "";
}

onUnmounted(() => clearTimeout(searchTimer));
</script>

<template>
  <div class="admin-stock">
    <div class="admin-stock__controls">
      <RpField v-model="text" class="admin-stock__search" :label="t['items.search']" type="search" />

      <div class="rp-field admin-stock__target">
        <label :for="`${countId}-target`" class="rp-field__label">{{ t["items.for"] }}</label>
        <select :id="`${countId}-target`" v-model="target" class="rp-field__input admin-select">
          <option v-for="player in targets" :key="player.number" :value="player.number">{{ describeTarget(player) }}</option>
        </select>
      </div>

      <div class="admin-stock__count">
        <RpField
          v-model="count"
          :label="t['field.count']"
          inputmode="numeric"
          :maxlength="4"
          :invalid="!!problem"
          :aria-describedby="problem ? problemId : undefined"
          @input="problem = ''"
        />
        <div class="admin-chips" role="group" :aria-label="t['items.quick']">
          <RpChip v-for="each in QUICK_COUNTS" :key="each" :pressed="count === String(each)" @click="pickCount(each)">{{ each }}</RpChip>
        </div>
      </div>
    </div>
    <p v-if="problem" :id="problemId" class="admin__problem" role="alert">{{ problem }}</p>

    <div class="admin-chips admin-stock__types" role="group" :aria-label="t['items.types']">
      <RpChip v-for="each in [ALL, ...ITEM_TYPES]" :key="each" :pressed="type === each" :data-type="each" @click="type = each">
        {{ each === ALL ? t["items.type.all"] : t[`items.type.${each}`] }}
      </RpChip>
    </div>

    <div class="admin-stock__body">
      <section class="admin-stock__found" :aria-label="t['items.found']">
        <ul v-if="searching && found.length" class="admin-items">
          <li v-for="entry in found" :key="entry.id" class="admin-item admin-stock__item">
            <span class="admin-item__name">{{ entry.name }}</span>
            <span class="admin-item__worn">{{ t[`items.type.${entry.type}`] ?? entry.type }}</span>
            <span class="admin-item__code">{{ entry.id }}</span>
            <RpButton class="admin-item__button" :disabled="!can('giveNow') || !targets.length" :data-item="entry.id" @click="give(entry)">
              {{ t["action.give"] }}
            </RpButton>
          </li>
        </ul>
        <p v-else-if="searching" class="admin__note">{{ t["card.no_items"] }}</p>
        <p v-else class="admin__note">{{ t["items.start"] }}</p>
        <p v-if="searching && found.length" class="admin__note">{{ t["items.more"] }}</p>
      </section>

      <aside class="admin-stock__recent">
        <h3 class="admin__heading">{{ t["items.recent"] }}</h3>
        <ul v-if="recent.length" class="admin-items">
          <li v-for="entry in recent" :key="entry.id" class="admin-item admin-stock__item admin-stock__item--recent">
            <span class="admin-item__name">{{ entry.name }}</span>
            <RpButton class="admin-item__button" :disabled="!can('giveNow') || !targets.length" @click="give(entry)">{{ t["action.give"] }}</RpButton>
          </li>
        </ul>
        <p v-else class="admin__note">{{ t["items.no_recent"] }}</p>
      </aside>
    </div>
  </div>
</template>
