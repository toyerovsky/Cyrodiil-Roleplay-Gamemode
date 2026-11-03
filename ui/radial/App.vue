<script setup>
import { nextTick, onMounted, onUnmounted, ref, useTemplateRef } from "vue";
import { RpButton, RpRadial } from "../rp/index.js";
import { usePageTexts } from "../shared/locale.js";
import { isComposing, isHeldPress } from "../shared/press.js";

const props = defineProps({ bridge: { type: Object, required: true } });

const { t } = usePageTexts(props.bridge, "radial");

const wheel = ref(null);
const highlight = ref(null);
const visit = ref(0);
const radial = useTemplateRef("radial");
let openedBy = "";

const findItem = (id) => wheel.value?.items.find((item) => item.id === id);

async function open(view, key) {
  wheel.value = view;
  openedBy = key.toLowerCase();
  highlight.value = null;
  visit.value++;
  await nextTick();
  radial.value.focus();
}

function choose(id) {
  const item = findItem(id);
  if (!item || item.disabled) return;
  wheel.value = null;
  props.bridge.pick(id);
}

function close() {
  if (!wheel.value) return;
  wheel.value = null;
  props.bridge.close();
}

function onKeydown(event) {
  if (!wheel.value || isComposing(event)) return;
  if (isHeldPress(event)) {
    event.preventDefault();
    return;
  }

  const key = event.key.toLowerCase();
  const number = Number.parseInt(key, 10);
  if (key === "escape" || key === openedBy) {
    event.preventDefault();
    close();
  } else if (number >= 1 && number <= wheel.value.items.length) {
    event.preventDefault();
    choose(wheel.value.items[number - 1].id);
  }
}

onMounted(() => {
  props.bridge.onWheel(open);
  props.bridge.onHidden(() => (wheel.value = null));
  window.addEventListener("keydown", onKeydown);
  props.bridge.signalReady();
});

onUnmounted(() => window.removeEventListener("keydown", onKeydown));
</script>

<template>
  <!-- The right mouse button closes it anywhere, on the wheel too, as the game's own wheels do. -->
  <div v-if="wheel" class="radial" @click.self="close" @contextmenu.prevent="close">
    <RpRadial
      ref="radial"
      :key="visit"
      v-model="highlight"
      :items="wheel.items"
      :title="wheel.title"
      :label="t.wheel"
      @select="choose"
    />
    <p class="radial__hint">{{ t.hint }}</p>
    <RpButton class="radial__close" @click="close">{{ t.close }}</RpButton>
  </div>
</template>
