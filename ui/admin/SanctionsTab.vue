<script setup>
import { RpButton } from "../rp/index.js";
import { fill } from "../shared/locale.js";
import { LIFTS } from "./actions.js";
import { formatDate, formatMoment } from "./format.js";

const props = defineProps({
  sanctions: { type: Array, default: null },
  t: { type: Object, required: true },
  locale: { type: String, required: true },
  may: { type: Function, required: true },
  can: { type: Function, required: true },
});

const emit = defineEmits(["act"]);

// A ban by the account, a condition by the number while its player is online and by the account while away.
function lift(sanction) {
  const action = LIFTS[sanction.kind];
  const values = { account: sanction.account, who: sanction.online ?? sanction.account };
  emit("act", action, values, sanction.character ?? sanction.nickname);
}

const formatWho = (sanction) => (sanction.character ? `${sanction.character} (${sanction.nickname})` : sanction.nickname);
</script>

<template>
  <p v-if="!sanctions" class="admin__note" role="status">{{ t.loading }}</p>
  <p v-else-if="!sanctions.length" class="admin__note">{{ t["sanctions.none"] }}</p>
  <div v-else class="admin-table-scroll">
    <table class="admin-table">
      <thead>
        <tr>
          <th scope="col">{{ t["sanctions.kind"] }}</th>
          <th scope="col">{{ t["sanctions.who"] }}</th>
          <th scope="col">{{ t["sanctions.reason"] }}</th>
          <th scope="col">{{ t["sanctions.by"] }}</th>
          <th scope="col">{{ t["sanctions.until"] }}</th>
          <th scope="col"><span class="admin__hidden">{{ t["action.lift"] }}</span></th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="sanction in sanctions" :key="`${sanction.kind}-${sanction.account}`" :data-kind="sanction.kind">
          <td class="admin-table__kind">{{ t[`kind.${sanction.kind}`] ?? sanction.kind }}</td>
          <td>
            {{ formatWho(sanction) }}
            <span v-if="sanction.online !== null" class="admin-table__meta">{{ fill(t["sanctions.online"], { number: sanction.online }) }}</span>
          </td>
          <td>{{ sanction.reason }}</td>
          <td>{{ sanction.by }}</td>
          <td>
            {{ sanction.until === null ? t["sanctions.until_lifted"] : formatMoment(sanction.until, locale) }}
            <span v-if="sanction.since !== null" class="admin-table__meta">{{ fill(t["sanctions.since"], { date: formatDate(sanction.since, locale) }) }}</span>
          </td>
          <td>
            <RpButton v-if="may(LIFTS[sanction.kind])" :disabled="!can(LIFTS[sanction.kind])" @click="lift(sanction)">{{ t["action.lift"] }}</RpButton>
          </td>
        </tr>
      </tbody>
    </table>
  </div>
</template>
