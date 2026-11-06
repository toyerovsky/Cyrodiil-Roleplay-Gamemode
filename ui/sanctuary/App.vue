<script setup>
import { computed, onMounted, ref } from "vue";
import { RpIcon } from "../rp/index.js";
import { fill, usePageTexts } from "../shared/locale.js";
import { formatLeft, useCountdown } from "./clock.js";

const props = defineProps({ bridge: { type: Object, required: true } });

const { t } = usePageTexts(props.bridge, "sanctuary");
const state = ref(null);
const { left, start } = useCountdown();

const shown = computed(() => state.value?.inside === true);
const aggressor = computed(() => shown.value && state.value.aggressor);

onMounted(() => {
  props.bridge.onState((view) => {
    state.value = view;
    start(view.aggressor ? view.secondsLeft : 0);
  });
  props.bridge.signalReady();
});
</script>

<template>
  <section v-if="shown" class="sanctuary" :class="{ 'sanctuary--aggressor': aggressor }" :aria-label="t.title" role="status">
    <RpIcon class="sanctuary__shield" name="shield" />
    <div class="sanctuary__text">
      <p class="sanctuary__title">{{ t.title }}</p>
      <p class="sanctuary__state">
        {{ aggressor ? t.aggressor : t.protected }}
        <span v-if="aggressor" class="sanctuary__left">{{ fill(t.left, { time: formatLeft(left) }) }}</span>
      </p>
    </div>
  </section>
</template>
