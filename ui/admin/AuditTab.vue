<script setup>
import { reactive } from "vue";
import { RpButton, RpField } from "../rp/index.js";
import { fill } from "../shared/locale.js";
import { formatMoment } from "./format.js";

const props = defineProps({
  audit: { type: Object, default: null },
  t: { type: Object, required: true },
  locale: { type: String, required: true },
});

const emit = defineEmits(["search"]);

const filter = reactive({ action: "", actor: "", target: "", from: "", to: "" });

// Midnight on the staff member's own clock, not in UTC.
const parseDay = (value) => (value ? new Date(`${value}T00:00`).getTime() : null);
const trimOrNull = (value) => value.trim() || null;

function search() {
  emit("search", { action: trimOrNull(filter.action), actor: trimOrNull(filter.actor), target: trimOrNull(filter.target), from: parseDay(filter.from), to: parseDay(filter.to) });
}

const loadOlder = () => emit("search", null, props.audit.entries.at(-1).id);

const formatWho = (entry) => (entry.target ? `${entry.actor ?? ""} ${fill(props.t["audit.on"], { target: entry.target })}`.trim() : entry.actor);
</script>

<template>
  <div class="admin-audit">
    <form class="admin-audit__filter" @submit.prevent="search">
      <RpField v-model="filter.action" :label="t['audit.action']" />
      <RpField v-model="filter.actor" :label="t['audit.actor']" />
      <RpField v-model="filter.target" :label="t['audit.target']" />
      <RpField v-model="filter.from" :label="t['audit.from']" type="date" />
      <RpField v-model="filter.to" :label="t['audit.to']" type="date" />
      <RpButton type="submit" variant="primary">{{ t["audit.search"] }}</RpButton>
    </form>

    <p v-if="!audit" class="admin__note" role="status">{{ t.loading }}</p>
    <p v-else-if="!audit.entries.length" class="admin__note">{{ t["audit.none"] }}</p>
    <template v-else>
      <ol class="admin-entries">
        <li v-for="entry in audit.entries" :key="entry.id" class="admin-entry">
          <span class="admin-entry__at">{{ formatMoment(entry.at, locale) }}</span>
          <span class="admin-entry__action">{{ entry.action }}</span>
          <span class="admin-entry__who">{{ formatWho(entry) }}</span>
          <span v-if="entry.details" class="admin-entry__details">{{ entry.details }}</span>
        </li>
      </ol>
      <RpButton v-if="audit.more" class="admin-audit__older" @click="loadOlder">{{ t["audit.older"] }}</RpButton>
    </template>
  </div>
</template>
