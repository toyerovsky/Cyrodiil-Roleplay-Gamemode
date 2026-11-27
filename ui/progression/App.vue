<script setup>
import { onMounted, ref } from "vue";
import { fill, usePageTexts } from "../shared/locale.js";
const props = defineProps({ bridge: { type: Object, required: true } });
const { t } = usePageTexts(props.bridge, "progression");
const state = ref({ enabled: false });
onMounted(() => {
  props.bridge.onState((view) => { state.value = view; });
  props.bridge.signalReady();
});
</script>

<template>
  <section v-if="state.enabled" class="progression rp-plate" :aria-label="t.title">
    <div class="progression__line">
      <span class="progression__level">{{ fill(t.level, { level: state.level }) }}</span>
      <span>{{ state.next ? fill(t.xp, { xp: state.xp, next: state.next }) : t.maximum }}</span>
    </div>
    <div class="progression__bar" role="progressbar" :aria-label="t.title" aria-valuemin="0"
      :aria-valuemax="state.next || 1" :aria-valuenow="state.next ? state.xp : 1" :aria-valuetext="state.next ? undefined : t.maximum">
      <div class="progression__fill" :style="{ width: `${state.next ? Math.min(100, state.xp / state.next * 100) : 100}%` }"></div>
    </div>
    <div v-if="state.perkPoints" class="progression__perks">{{ fill(t.perks, { points: state.perkPoints }) }}</div>
  </section>
</template>
