<script setup>
import { RpButton } from "../rp/index.js";
import { fill } from "../shared/locale.js";

defineProps({
  view: { type: Object, required: true },
  t: { type: Object, required: true },
  busy: Boolean,
  showLeft: { type: Function, required: true },
  showNumber: { type: Function, required: true },
  describe: { type: Function, required: true },
});

defineEmits(["withdraw"]);
</script>

<template>
  <div class="auction-mine">
    <section class="auction-mine__part" :aria-label="t.selling">
      <h2 class="auction__heading">{{ t.selling }}</h2>
      <ul v-if="view.selling.length" class="auction__list">
        <li v-for="lot in view.selling" :key="lot.id" class="auction-row auction-row--still" :data-lot="lot.id">
          <span class="auction-row__name">{{ describe(lot) }}</span>
          <span class="auction-row__facts">
            <span v-if="lot.topBid !== null">{{ fill(t.top_bid, { count: showNumber(lot.topBid) }) }}</span>
            <span v-else-if="lot.startPrice !== null">{{ fill(t.start_price, { count: showNumber(lot.startPrice) }) }}</span>
            <span v-if="lot.buyoutPrice !== null">{{ fill(t.buyout_price, { count: showNumber(lot.buyoutPrice) }) }}</span>
          </span>
          <span class="auction-row__left">{{ showLeft(lot) }}</span>
          <span v-if="lot.bids > 0" class="auction-row__mark">{{ t.has_bids }}</span>
          <RpButton
            v-else
            :disabled="busy"
            :aria-label="fill(t.withdraw_named, { name: describe(lot) })"
            data-action="withdraw"
            @click="$emit('withdraw', lot.id)"
          >
            {{ t.withdraw }}
          </RpButton>
        </li>
      </ul>
      <p v-else class="auction__empty">{{ t.selling_none }}</p>
    </section>

    <section class="auction-mine__part" :aria-label="t.bidding">
      <h2 class="auction__heading">{{ t.bidding }}</h2>
      <ul v-if="view.bidding.length" class="auction__list">
        <li v-for="lot in view.bidding" :key="lot.id" class="auction-row auction-row--still" :data-lot="lot.id">
          <span class="auction-row__name">{{ describe(lot) }}</span>
          <span class="auction-row__facts">
            <span>{{ fill(t.your_top, { count: showNumber(lot.topBid) }) }}</span>
            <span v-if="lot.buyoutPrice !== null">{{ fill(t.buyout_price, { count: showNumber(lot.buyoutPrice) }) }}</span>
          </span>
          <span class="auction-row__left">{{ showLeft(lot) }}</span>
        </li>
      </ul>
      <p v-else class="auction__empty">{{ t.bidding_none }}</p>
    </section>
  </div>
</template>
