<script setup>
import { computed, ref } from "vue";
import { RpField } from "../rp/index.js";
import { fill } from "../shared/locale.js";

// What the player can still offer, each with `left`: what they carry beyond the offer. A click adds one piece.
const props = defineProps({
  items: { type: Array, required: true },
  t: { type: Object, required: true },
  describe: { type: Function, required: true },
  disabled: Boolean,
});

defineEmits(["add"]);

const query = ref("");

const shown = computed(() => {
  const wanted = query.value.trim().toLowerCase();
  return props.items.filter((item) => item.name.toLowerCase().includes(wanted));
});
const emptyText = computed(() => (props.items.length === 0 ? props.t.empty : props.t.no_match));
</script>

<template>
  <section class="exchange__side" :aria-label="t.your_items">
    <h2 class="exchange__heading">{{ t.your_items }}</h2>
    <RpField v-model="query" :label="t.filter" />
    <ul v-if="shown.length" class="exchange__list">
      <li v-for="item in shown" :key="item.key">
        <button
          type="button"
          class="exchange__item"
          :disabled="disabled || item.left === 0"
          :aria-label="fill(t.add, { name: describe(item) })"
          @click="$emit('add', item)"
        >
          <span class="exchange__name">{{ describe(item) }}</span>
          <span class="exchange__times">{{ fill(t.times, { count: item.left }) }}</span>
        </button>
      </li>
    </ul>
    <p v-else class="exchange__none">{{ emptyText }}</p>
    <p class="exchange__note">{{ t.not_listed }}</p>
  </section>
</template>
