<script setup>
import { computed, ref, watch } from "vue";
import { RpButton, RpField } from "../rp/index.js";
import { fill } from "../shared/locale.js";
import { PLAYER_ACTIONS, WORLD_ACTIONS } from "./actions.js";
import { isUnknownItem, SEARCH_FROM } from "./items.js";
import { formatPlayed } from "../shared/format.js";
import { formatDate, formatMoment } from "./format.js";

const props = defineProps({
  player: { type: Object, required: true },
  card: { type: Object, default: null },
  found: { type: Array, required: true },
  t: { type: Object, required: true },
  locale: { type: String, required: true },
  may: { type: Function, required: true },
  can: { type: Function, required: true },
});

const emit = defineEmits(["act", "search"]);

// Of a pair, the one that fits the player shows: a frozen player gets Unfreeze, not Freeze. Revive only for the dead,
// health only for the living.
const FITS = {
  freeze: (player) => !player.frozen,
  unfreeze: (player) => player.frozen,
  mute: (player) => !player.muted,
  unmute: (player) => player.muted,
  revive: (player) => player.health === 0,
  health: (player) => player.health > 0,
};
// What a dialog starts from: the health's most, the roles held now.
const DIALOG_VALUES = {
  health: (player) => ({ most: player.healthMax }),
  roles: (player) => ({ current: player.roles }),
};

const inWorld = (player) => player.character && player.area !== null;

const item = ref("");

const actions = computed(() =>
  PLAYER_ACTIONS.filter(
    (action) => props.may(action) && (FITS[action]?.(props.player) ?? true) && (inWorld(props.player) || !WORLD_ACTIONS.includes(action)),
  ),
);
const label = computed(() => `[${props.player.number}] ${props.player.name}`);

watch(item, (text) => emit("search", text.trim().length >= SEARCH_FROM ? { text: text.trim(), type: "" } : null));
watch(() => props.player.number, () => (item.value = ""));

const act = (action, values = {}) => emit("act", action, { number: props.player.number, ...values }, props.player.name);
</script>

<template>
  <section class="admin-card" :aria-label="label">
    <header class="admin-card__head">
      <h2 class="admin-card__name">{{ label }}</h2>
      <p class="admin-card__nickname">{{ player.nickname }}</p>
      <p v-if="player.roles.length" class="admin-card__roles">{{ fill(t["card.roles"], { roles: player.roles.join(", ") }) }}</p>
    </header>

    <div v-if="actions.length" class="admin-card__actions" role="group" :aria-label="t['card.actions']">
      <RpButton
        v-for="action in actions"
        :key="action"
        :disabled="!can(action)"
        :data-action="action"
        @click="act(action, DIALOG_VALUES[action]?.(player))"
      >
        {{ t[`action.${action}`] }}
      </RpButton>
    </div>

    <p v-if="!card" class="admin__note" role="status">{{ t.loading }}</p>
    <template v-else>
      <dl class="admin-facts">
        <div class="admin-fact">
          <dt>{{ t["card.account"] }}</dt>
          <dd class="admin-fact__code">{{ card.account }}</dd>
        </div>
        <div class="admin-fact">
          <dt>{{ t["card.account_since"] }}</dt>
          <dd>{{ formatDate(card.accountCreatedAt, locale) }}</dd>
        </div>
        <div class="admin-fact">
          <dt>{{ t["card.previous"] }}</dt>
          <dd>{{ card.previousVisitAt === null ? t["card.first_visit"] : formatMoment(card.previousVisitAt, locale) }}</dd>
        </div>
        <div v-if="card.characterCreatedAt !== null" class="admin-fact">
          <dt>{{ t["card.character_since"] }}</dt>
          <dd>{{ formatDate(card.characterCreatedAt, locale) }}</dd>
        </div>
        <div class="admin-fact">
          <dt>{{ t["card.played"] }}</dt>
          <dd>{{ formatPlayed(card.playedSeconds, t, locale) }}</dd>
        </div>
        <div class="admin-fact">
          <dt>{{ t["card.area"] }}</dt>
          <dd class="admin-fact__code">{{ card.area ?? t["card.away"] }}</dd>
        </div>
        <div v-if="card.position" class="admin-fact">
          <dt>{{ t["card.position"] }}</dt>
          <dd class="admin-fact__code">{{ card.position }}</dd>
        </div>
      </dl>

      <h3 class="admin__heading">{{ t["card.sanctions"] }}</h3>
      <ul v-if="card.sanctions.length" class="admin-sanctions">
        <li v-for="sanction in card.sanctions" :key="sanction.kind" class="admin-sanction">
          <strong>{{ t[`kind.${sanction.kind}`] ?? sanction.kind }}</strong>
          <span v-if="sanction.reason">{{ sanction.reason }}</span>
          <span class="admin-sanction__meta">
            {{ sanction.by }} · {{ sanction.until === null ? t["sanctions.until_lifted"] : formatMoment(sanction.until, locale) }}
          </span>
        </li>
      </ul>
      <p v-else class="admin__note">{{ t["card.no_sanctions"] }}</p>

      <h3 class="admin__heading">{{ t["card.inventory"] }}</h3>
      <p v-if="card.inventory === null" class="admin__note">{{ t["card.inventory_away"] }}</p>
      <template v-else>
        <ul v-if="card.inventory.length" class="admin-items">
          <li v-for="(entry, index) in card.inventory" :key="`${entry.id}-${index}`" class="admin-item">
            <span class="admin-item__name">{{ entry.name }}</span>
            <span class="admin-item__count">{{ entry.count.toLocaleString(locale) }}</span>
            <span class="admin-item__worn">{{ entry.worn ? t["card.worn"] : "" }}</span>
            <RpButton
              v-if="may('take') && !isUnknownItem(entry)"
              class="admin-item__button"
              :disabled="!can('take')"
              @click="act('take', { item: entry.id, most: entry.count })"
            >
              {{ t["action.take"] }}
            </RpButton>
          </li>
        </ul>
        <p v-else class="admin__note">{{ t["card.inventory_empty"] }}</p>

        <template v-if="may('give')">
          <h3 class="admin__heading">{{ t["card.give"] }}</h3>
          <RpField v-model="item" :label="t['card.item_search']" type="search" />
          <ul v-if="found.length" class="admin-items admin-items--found">
            <li v-for="entry in found" :key="entry.id" class="admin-item">
              <span class="admin-item__name">{{ entry.name }}</span>
              <span class="admin-item__code">{{ entry.id.toLowerCase() === entry.name.toLowerCase() ? "" : entry.id }}</span>
              <span class="admin-item__worn">{{ entry.type }}</span>
              <RpButton class="admin-item__button" :disabled="!can('give')" @click="act('give', { item: entry.id })">
                {{ t["action.give"] }}
              </RpButton>
            </li>
          </ul>
          <p v-else-if="item.trim().length >= SEARCH_FROM" class="admin__note">{{ t["card.no_items"] }}</p>
        </template>
      </template>

      <h3 class="admin__heading">{{ t["card.history"] }}</h3>
      <ol v-if="card.history.length" class="admin-entries">
        <li v-for="entry in card.history" :key="entry.id" class="admin-entry">
          <span class="admin-entry__at">{{ formatMoment(entry.at, locale) }}</span>
          <span class="admin-entry__action">{{ entry.action }}</span>
          <span class="admin-entry__who">{{ entry.actor }}</span>
          <span v-if="entry.details" class="admin-entry__details">{{ entry.details }}</span>
        </li>
      </ol>
      <p v-else class="admin__note">{{ t["card.no_history"] }}</p>
    </template>
  </section>
</template>
