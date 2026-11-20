<script setup>
import { computed, nextTick, ref, useTemplateRef, watch } from "vue";
import { RpButton, RpChip, RpField } from "../rp/index.js";
import { fill } from "../shared/locale.js";
import { findFee, readSum } from "./lots.js";

const props = defineProps({
  view: { type: Object, required: true },
  t: { type: Object, required: true },
  busy: Boolean,
  showNumber: { type: Function, required: true },
  describe: { type: Function, required: true },
});

const emit = defineEmits(["post"]);

const query = ref("");
const selectedKey = ref(null);
const count = ref("1");
const startPrice = ref("");
const buyoutPrice = ref("");
const hours = ref(props.view.terms.hours[0]);
// What the house would refuse, and the field it is about: count, start or buyout. Null while there is nothing to say.
const problem = ref(null);
const fields = {
  count: useTemplateRef("countField"),
  start: useTemplateRef("startField"),
  buyout: useTemplateRef("buyoutField"),
};

const terms = computed(() => props.view.terms);
const full = computed(() => props.view.selling.length >= terms.value.maxLots);
const shown = computed(() => {
  const wanted = query.value.trim().toLowerCase();
  return props.view.items.filter((item) => item.name.toLowerCase().includes(wanted));
});
const selected = computed(() => props.view.items.find((item) => item.key === selectedKey.value) ?? null);
const highest = computed(() => Math.max(readSum(startPrice.value) ?? 0, readSum(buyoutPrice.value) ?? 0));
const fee = computed(() => findFee(highest.value, terms.value.feePercent));

// An item no longer carried is no longer picked.
watch(selected, (item) => {
  if (!item) selectedKey.value = null;
});

async function select(item) {
  selectedKey.value = item.key;
  count.value = "1";
  problem.value = null;
  await nextTick();
  fields.count.value?.focus();
}

// Null when the lot may go to the house, which checks it again.
function check(item) {
  const max = terms.value.maxPrice;
  const pieces = readSum(count.value);
  if (pieces === null || pieces < 1 || pieces > item.count) return { field: "count", text: fill(props.t.count_invalid, { count: item.count }) };

  const start = readSum(startPrice.value);
  const buyout = readSum(buyoutPrice.value);
  if (start === null || start > max) return { field: "start", text: fill(props.t.price_invalid, { max: props.showNumber(max) }) };
  if (buyout === null || buyout > max) return { field: "buyout", text: fill(props.t.price_invalid, { max: props.showNumber(max) }) };
  if (start === 0 && buyout === 0) return { field: "start", text: props.t.price_missing };
  if (start > 0 && buyout > 0 && buyout <= start) return { field: "buyout", text: props.t.buyout_low };

  const cost = findFee(Math.max(start, buyout), terms.value.feePercent);
  return cost > props.view.gold ? { field: "start", text: fill(props.t.fee_poor, { count: props.showNumber(cost), gold: props.showNumber(props.view.gold) }) } : null;
}

function post() {
  const item = selected.value;
  problem.value = check(item);
  if (problem.value) {
    fields[problem.value.field].value.focus();
    return;
  }

  emit("post", {
    item: item.key,
    count: readSum(count.value),
    startPrice: readSum(startPrice.value),
    buyoutPrice: readSum(buyoutPrice.value),
    hours: hours.value,
  });
}
</script>

<template>
  <div class="auction-sell">
    <section class="auction-sell__carried" :aria-label="t.carried">
      <h2 class="auction__heading">{{ t.carried }}</h2>
      <RpField v-model="query" :label="t.filter" autocomplete="off" />
      <ul v-if="shown.length" class="auction__list">
        <li v-for="item in shown" :key="item.key">
          <button
            type="button"
            class="auction-row"
            :class="{ 'auction-row--on': item.key === selectedKey }"
            :aria-pressed="item.key === selectedKey"
            :data-item="item.key"
            @click="select(item)"
          >
            <span class="auction-row__name">{{ describe(item) }}</span>
          </button>
        </li>
      </ul>
      <p v-else class="auction__empty">{{ t.nothing_carried }}</p>
    </section>

    <form v-if="selected" class="auction-detail auction-sell__form" novalidate :aria-label="describe(selected)" @submit.prevent="post">
      <h2 class="auction-detail__name">{{ describe(selected) }}</h2>
      <div class="auction-sell__count">
        <RpField
          ref="countField"
          v-model="count"
          :label="t.count"
          :invalid="problem?.field === 'count'"
          inputmode="numeric"
          autocomplete="off"
          :maxlength="String(selected.count).length"
          data-field="count"
        />
        <RpChip v-if="selected.count > 1" data-fill="all" @click="count = String(selected.count)">{{ fill(t.all, { count: selected.count }) }}</RpChip>
      </div>
      <RpField
        ref="startField"
        v-model="startPrice"
        :label="t.start"
        :invalid="problem?.field === 'start'"
        inputmode="numeric"
        autocomplete="off"
        :maxlength="String(terms.maxPrice).length"
        data-field="start"
      />
      <RpField
        ref="buyoutField"
        v-model="buyoutPrice"
        :label="t.buyout"
        :invalid="problem?.field === 'buyout'"
        inputmode="numeric"
        autocomplete="off"
        :maxlength="String(terms.maxPrice).length"
        data-field="buyout"
      />
      <div class="auction__chips" role="group" :aria-label="t.hours">
        <span class="auction__chips-label">{{ t.hours }}</span>
        <RpChip v-for="each in terms.hours" :key="each" :pressed="hours === each" :data-hours="each" @click="hours = each">
          {{ fill(t.hours_option, { hours: each }) }}
        </RpChip>
      </div>
      <p class="auction-detail__fact" data-fee>{{ fill(t.fee, { count: showNumber(fee), percent: terms.cutPercent }) }}</p>
      <p class="auction-detail__fact">{{ fill(t.lots_used, { count: view.selling.length, max: terms.maxLots }) }}</p>
      <p v-if="full" class="auction__problem" role="alert">{{ fill(t.too_many, { max: terms.maxLots }) }}</p>
      <p v-else-if="problem" class="auction__problem" role="alert">{{ problem.text }}</p>
      <RpButton type="submit" variant="primary" :disabled="busy || full" :aria-busy="busy" data-action="post">{{ t.post }}</RpButton>
    </form>
    <p v-else class="auction-detail auction-detail--empty">{{ t.pick_item }}</p>
  </div>
</template>
