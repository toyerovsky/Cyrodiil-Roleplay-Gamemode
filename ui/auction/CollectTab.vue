<script setup>
import { RpButton } from "../rp/index.js";
import { fill } from "../shared/locale.js";

const props = defineProps({
  view: { type: Object, required: true },
  t: { type: Object, required: true },
  busy: Boolean,
  showGold: { type: Function, required: true },
  describe: { type: Function, required: true },
});

defineEmits(["collect"]);

// The parcel id the page sends for every parcel at once (AuctionHouse.cs).
const EVERY = 0;

const describeParcel = (parcel) => (parcel.gold > 0 ? props.showGold(parcel.gold) : props.describe(parcel));
</script>

<template>
  <div class="auction-collect">
    <div v-if="view.parcels.length" class="auction-collect__all">
      <RpButton variant="primary" :disabled="busy" :aria-busy="busy" data-action="collect-all" @click="$emit('collect', EVERY)">{{ t.collect_all }}</RpButton>
    </div>
    <ul v-if="view.parcels.length" class="auction__list">
      <li v-for="parcel in view.parcels" :key="parcel.id" class="auction-row auction-row--still" :data-parcel="parcel.id">
        <span class="auction-row__name">{{ describeParcel(parcel) }}</span>
        <span class="auction-row__facts">{{ t[`reason.${parcel.reason}`] }}</span>
        <RpButton
          :disabled="busy"
          :aria-label="fill(t.collect_named, { name: describeParcel(parcel) })"
          data-action="collect"
          @click="$emit('collect', parcel.id)"
        >
          {{ t.collect }}
        </RpButton>
      </li>
    </ul>
    <p v-else class="auction__empty">{{ t.nothing_waits }}</p>
  </div>
</template>
