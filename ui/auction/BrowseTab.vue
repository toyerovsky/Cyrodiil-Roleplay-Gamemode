<script setup>
import { computed, ref, useTemplateRef, watch } from "vue";
import { RpButton, RpChip, RpField } from "../rp/index.js";
import { fill } from "../shared/locale.js";
import { CATEGORIES, readSum } from "./lots.js";

const props = defineProps({
  view: { type: Object, required: true },
  t: { type: Object, required: true },
  busy: Boolean,
  showLeft: { type: Function, required: true },
  showNumber: { type: Function, required: true },
  describe: { type: Function, required: true },
});

const emit = defineEmits(["search", "bid", "buy"]);

const text = ref(props.view.browse.text);
// The kind last asked for, so a search sent before the house answered the last one keeps it.
const category = ref(props.view.browse.category);
const selectedId = ref(null);
const amount = ref("");
const problem = ref(null);
const searchField = useTemplateRef("searchField");
const amountField = useTemplateRef("amountField");

const browse = computed(() => props.view.browse);
const selected = computed(() => browse.value.lots.find((lot) => lot.id === selectedId.value) ?? null);
const filtered = computed(() => browse.value.text !== "" || browse.value.category !== "All");
// A lot whose next bid would reach its buy now price is only bought now.
const canBid = computed(() => {
  const lot = selected.value;
  return lot && !lot.mine && !lot.leading && lot.startPrice !== null && (lot.buyoutPrice === null || lot.minimumBid < lot.buyoutPrice);
});
const canBuy = computed(() => selected.value && !selected.value.mine && selected.value.buyoutPrice !== null);

watch(() => props.view.browse.category, (shown) => { category.value = shown; });

// A lot picked, or outbid since, gets the least bid it takes. The same lot shown again keeps what was typed.
watch(() => `${selected.value?.id}:${selected.value?.minimumBid}`, () => {
  amount.value = selected.value?.minimumBid ? String(selected.value.minimumBid) : "";
  problem.value = null;
});

function find(page) {
  emit("search", text.value.trim(), category.value, page);
}

function pickCategory(picked) {
  category.value = picked;
  find(0);
}

function select(lot) {
  selectedId.value = lot.id;
}

// Null when the bid may go to the house, which checks it again.
function checkBid(wanted, lot) {
  const max = props.view.terms.maxPrice;
  if (wanted === null || wanted < lot.minimumBid || wanted > max) return fill(props.t.bid_invalid, { min: props.showNumber(lot.minimumBid), max: props.showNumber(max) });
  if (lot.buyoutPrice !== null && wanted >= lot.buyoutPrice) return fill(props.t.bid_buyout, { count: props.showNumber(lot.buyoutPrice) });
  return wanted > props.view.gold ? fill(props.t.too_poor, { count: props.showNumber(props.view.gold) }) : null;
}

function bid() {
  const lot = selected.value;
  const wanted = readSum(amount.value);
  problem.value = checkBid(wanted, lot);
  if (problem.value) {
    amountField.value.focus();
    return;
  }

  emit("bid", lot.id, wanted);
}

function buy() {
  const lot = selected.value;
  problem.value = lot.buyoutPrice > props.view.gold ? fill(props.t.too_poor, { count: props.showNumber(props.view.gold) }) : null;
  if (!problem.value) emit("buy", lot.id, lot.buyoutPrice);
}

defineExpose({ focus: () => searchField.value.focus() });
</script>

<template>
  <div class="auction-browse">
    <form class="auction-browse__search" role="search" @submit.prevent="find(0)">
      <RpField ref="searchField" v-model="text" :label="t.search" maxlength="64" autocomplete="off" />
      <RpButton type="submit">{{ t.search_go }}</RpButton>
    </form>

    <div class="auction__chips" role="group" :aria-label="t.categories">
      <RpChip v-for="each in CATEGORIES" :key="each" :pressed="category === each" :data-category="each" @click="pickCategory(each)">
        {{ t[`category.${each}`] }}
      </RpChip>
    </div>

    <div class="auction-browse__body">
      <div class="auction-browse__lots">
        <ul v-if="browse.lots.length" class="auction__list" :aria-label="t.lots">
          <li v-for="lot in browse.lots" :key="lot.id">
            <button
              type="button"
              class="auction-row"
              :class="{ 'auction-row--on': lot.id === selectedId }"
              :aria-pressed="lot.id === selectedId"
              :data-lot="lot.id"
              @click="select(lot)"
            >
              <span class="auction-row__name">{{ describe(lot) }}</span>
              <span v-if="lot.mine" class="auction-row__mark">{{ t.yours }}</span>
              <span v-else-if="lot.leading" class="auction-row__mark auction-row__mark--leading">{{ t.leading }}</span>
              <span class="auction-row__facts">
                <span v-if="lot.topBid !== null">{{ fill(t.top_bid, { count: showNumber(lot.topBid) }) }}</span>
                <span v-else-if="lot.startPrice !== null">{{ fill(t.start_price, { count: showNumber(lot.startPrice) }) }}</span>
                <span v-if="lot.buyoutPrice !== null">{{ fill(t.buyout_price, { count: showNumber(lot.buyoutPrice) }) }}</span>
              </span>
              <span class="auction-row__left">{{ showLeft(lot) }}</span>
            </button>
          </li>
        </ul>
        <p v-else class="auction__empty">{{ filtered ? t.no_match : t.none }}</p>

        <div v-if="browse.page > 0 || browse.more" class="auction-browse__pages">
          <RpButton :disabled="browse.page === 0" data-action="previous" @click="find(browse.page - 1)">{{ t.previous }}</RpButton>
          <span>{{ fill(t.page, { page: browse.page + 1 }) }}</span>
          <RpButton :disabled="!browse.more" data-action="next" @click="find(browse.page + 1)">{{ t.next }}</RpButton>
        </div>
      </div>

      <section v-if="selected" class="auction-detail" :aria-label="describe(selected)">
        <h2 class="auction-detail__name">{{ describe(selected) }}</h2>
        <p class="auction-detail__facts">
          <span>{{ t[`category.${selected.category}`] }}</span>
          <span>{{ fill(t.bids, { count: selected.bids }) }}</span>
          <span>{{ showLeft(selected) }}</span>
        </p>
        <p v-if="selected.topBid !== null" class="auction-detail__price">{{ fill(t.top_bid, { count: showNumber(selected.topBid) }) }}</p>
        <p v-else-if="selected.startPrice !== null" class="auction-detail__price">{{ fill(t.start_price, { count: showNumber(selected.startPrice) }) }}</p>
        <p v-if="selected.buyoutPrice !== null" class="auction-detail__price">{{ fill(t.buyout_price, { count: showNumber(selected.buyoutPrice) }) }}</p>

        <p v-if="selected.mine" class="auction-detail__note">{{ t.own_lot }}</p>
        <p v-else-if="selected.leading" class="auction-detail__note">{{ t.leading_note }}</p>

        <form v-if="canBid" class="auction-detail__bid" novalidate @submit.prevent="bid">
          <RpField
            ref="amountField"
            v-model="amount"
            :label="t.your_bid"
            :invalid="!!problem"
            inputmode="numeric"
            autocomplete="off"
            :maxlength="String(view.terms.maxPrice).length"
            data-field="bid"
          />
          <p v-if="problem" class="auction__problem" role="alert">{{ problem }}</p>
          <p v-else class="auction-detail__fact">{{ fill(t.minimum, { count: showNumber(selected.minimumBid) }) }}</p>
          <RpButton type="submit" variant="primary" :disabled="busy" :aria-busy="busy" data-action="bid">{{ t.bid }}</RpButton>
        </form>
        <RpButton v-if="canBuy" :variant="canBid ? 'default' : 'primary'" :disabled="busy" :aria-busy="busy" data-action="buy" @click="buy">
          {{ fill(t.buy, { count: showNumber(selected.buyoutPrice) }) }}
        </RpButton>
        <p v-if="problem && !canBid" class="auction__problem" role="alert">{{ problem }}</p>
      </section>
      <p v-else class="auction-detail auction-detail--empty">{{ t.pick_lot }}</p>
    </div>
  </div>
</template>
