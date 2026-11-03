<script setup>
import { onMounted, onUnmounted, ref } from "vue";
import { usePageTexts } from "../shared/locale.js";

const props = defineProps({ bridge: { type: Object, required: true } });

const { t } = usePageTexts(props.bridge, "frozen");
const notice = ref(null);

// Only the staff let the player go, no key does, and the game gets none of them.
function onKeydown(event) {
  if (notice.value) event.preventDefault();
}

onMounted(() => {
  props.bridge.onNotice((text) => (notice.value = text));
  props.bridge.onThawed(() => (notice.value = null));
  window.addEventListener("keydown", onKeydown);
  props.bridge.signalReady();
});

onUnmounted(() => window.removeEventListener("keydown", onKeydown));
</script>

<template>
  <div v-if="notice" class="frozen">
    <section class="frozen__plate" role="alert" aria-labelledby="frozen-title">
      <h1 id="frozen-title" class="frozen__title">{{ t.title }}</h1>
      <p class="frozen__notice">{{ notice }}</p>
      <p class="frozen__hint">{{ t.hint }}</p>
    </section>
  </div>
</template>
