<script setup>
import { fill } from "../shared/locale.js";
import { formatDay, formatLength, formatWeight } from "./sizes.js";

// A species of the records board (a FisherSpeciesView): its longest fish by rank, the reader's own marked, and the
// reader's best under them when it is not among them.
const props = defineProps({
  species: { type: Object, required: true },
  t: { type: Object, required: true },
  locale: { type: String, required: true },
});

const COLUMNS = ["rank", "name", "length", "weight", "date"];

const showRank = (row) => fill(props.t["records.rank"], { rank: row.rank.toLocaleString(props.locale) });
</script>

<template>
  <p v-if="species.top.length === 0" class="fisher__no-records" data-no-records>{{ t["records.none"] }}</p>
  <table v-else class="fisher__records" data-records>
    <caption>{{ t["records.caption"] }}</caption>
    <thead>
      <tr>
        <th v-for="column in COLUMNS" :key="column" scope="col" :class="`fisher__record-${column}`">{{ t[`records.column.${column}`] }}</th>
      </tr>
    </thead>
    <tbody>
      <tr v-for="(row, index) in species.top" :key="index" :class="{ 'fisher__record--mine': row.mine }" :data-mine="row.mine || undefined">
        <td class="fisher__record-rank">{{ showRank(row) }}</td>
        <td class="fisher__record-name"><span>{{ row.name }}</span> <span v-if="row.mine" class="fisher__tag">{{ t["records.you"] }}</span></td>
        <td class="fisher__record-length">{{ formatLength(row.lengthCm, t, locale) }}</td>
        <td class="fisher__record-weight">{{ formatWeight(row.grams, t, locale) }}</td>
        <td class="fisher__record-date">{{ formatDay(row.at, locale) }}</td>
      </tr>
    </tbody>
    <tfoot v-if="species.own">
      <tr class="fisher__record--mine" data-own>
        <td class="fisher__record-rank">{{ showRank(species.own) }}</td>
        <td class="fisher__record-name">{{ t["records.own"] }}</td>
        <td class="fisher__record-length">{{ formatLength(species.own.lengthCm, t, locale) }}</td>
        <td class="fisher__record-weight">{{ formatWeight(species.own.grams, t, locale) }}</td>
        <td class="fisher__record-date">{{ formatDay(species.own.at, locale) }}</td>
      </tr>
    </tfoot>
  </table>
</template>
