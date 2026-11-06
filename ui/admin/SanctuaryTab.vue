<script setup>
import { ref } from "vue";
import { RpButton, RpField } from "../rp/index.js";
import { formatLeft } from "../sanctuary/clock.js";
import { fill } from "../shared/locale.js";
import { formatMoment } from "./format.js";

// The Sanctuary as it runs (on or off, the aggressors online) over its incident log, newest first a page at a time as the
// audit. The staff read real names.
const props = defineProps({
  sanctuary: { type: Object, default: null },
  incidents: { type: Object, default: null },
  t: { type: Object, required: true },
  locale: { type: String, required: true },
  may: { type: Function, required: true },
  can: { type: Function, required: true },
});

const emit = defineEmits(["act", "search"]);

const name = ref("");

const search = () => emit("search", { name: name.value.trim() || null });
const loadOlder = () => emit("search", null, props.incidents.incidents.at(-1).id);
const turn = () => emit("act", props.sanctuary.enabled ? "sanctuaryOff" : "sanctuaryOn", {});
const clear = (aggressor) => emit("act", "sanctuaryClear", { number: aggressor.number }, aggressor.name);

function describe(incident) {
  const parts = [incident.areaName, fill(props.t["sanctuary.damage"], { damage: incident.damage })];
  if (incident.died) parts.push(props.t["sanctuary.died"]);
  parts.push(props.t[`sanctuary.verdict.${incident.verdict.toLowerCase()}`]);
  return parts.join(" · ");
}
</script>

<template>
  <div class="admin-sanctuary">
    <p v-if="!sanctuary" class="admin__note" role="status">{{ t.loading }}</p>
    <template v-else>
      <div class="admin-sanctuary__state" :class="{ 'admin-sanctuary__state--off': !sanctuary.enabled }">
        <p class="admin-sanctuary__running" role="status">{{ sanctuary.enabled ? t["sanctuary.on"] : t["sanctuary.off"] }}</p>
        <RpButton v-if="may('sanctuaryOn')" :disabled="!can('sanctuaryOn')" :variant="sanctuary.enabled ? 'default' : 'primary'" @click="turn">
          {{ sanctuary.enabled ? t["sanctuary.turn_off"] : t["sanctuary.turn_on"] }}
        </RpButton>
      </div>

      <section class="admin-sanctuary__aggressors">
        <h3 class="admin__heading">{{ t["sanctuary.aggressors"] }}</h3>
        <p v-if="!sanctuary.aggressors.length" class="admin__note">{{ t["sanctuary.no_aggressors"] }}</p>
        <ul v-else class="admin-items">
          <li v-for="aggressor in sanctuary.aggressors" :key="aggressor.number" class="admin-item admin-sanctuary__aggressor" :data-player="aggressor.number">
            <span class="admin-item__name">[{{ aggressor.number }}] {{ aggressor.name }}</span>
            <span class="admin-item__code">{{ aggressor.area ?? t["sanctuary.outside"] }}</span>
            <span class="admin-item__count">{{ fill(t["sanctuary.left"], { time: formatLeft(aggressor.secondsLeft) }) }}</span>
            <RpButton v-if="may('sanctuaryClear')" :disabled="!can('sanctuaryClear')" @click="clear(aggressor)">
              {{ t["action.sanctuaryClear"] }}
            </RpButton>
          </li>
        </ul>
      </section>
    </template>

    <h3 class="admin__heading">{{ t["sanctuary.incidents"] }}</h3>
    <form class="admin-dice__filter admin-sanctuary__filter" @submit.prevent="search">
      <p class="admin__note">{{ t["sanctuary.lead"] }}</p>
      <RpField v-model="name" :label="t['sanctuary.name']" />
      <RpButton type="submit" variant="primary">{{ t["sanctuary.search"] }}</RpButton>
    </form>

    <p v-if="!incidents" class="admin__note admin-sanctuary__list" role="status">{{ t.loading }}</p>
    <p v-else-if="!incidents.incidents.length" class="admin__note admin-sanctuary__list">{{ t["sanctuary.none"] }}</p>
    <ol v-else class="admin-entries admin-sanctuary__list">
      <li v-for="incident in incidents.incidents" :key="incident.id" class="admin-entry admin-incident" :class="{ 'admin-incident--died': incident.died }">
        <span class="admin-entry__at">{{ formatMoment(incident.at, locale) }}</span>
        <span class="admin-entry__action">{{ incident.victim }}</span>
        <span class="admin-entry__who">{{ describe(incident) }}</span>
        <span v-if="incident.aggressors.length" class="admin-entry__details">{{ fill(t["sanctuary.by"], { names: incident.aggressors.join(", ") }) }}</span>
      </li>
    </ol>
    <RpButton v-if="incidents?.more" class="admin-audit__older" @click="loadOlder">{{ t["sanctuary.older"] }}</RpButton>
  </div>
</template>
