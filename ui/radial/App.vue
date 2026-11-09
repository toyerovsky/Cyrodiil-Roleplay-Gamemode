<script setup>
import { computed, nextTick, onMounted, onUnmounted, ref, useTemplateRef } from "vue";
import { RpButton, RpRadial } from "../rp/index.js";
import { fill, usePageTexts } from "../shared/locale.js";
import { isComposing, isHeldPress } from "../shared/press.js";
import { NEXT, PAGE_KEYS, PREVIOUS, countPages, layPage, turnPage } from "./paging.js";

const props = defineProps({ bridge: { type: Object, required: true } });

const { t } = usePageTexts(props.bridge, "radial");

// Half a wedge clockwise: Next, the first, just right of twelve, and Previous, the last, just left.
const PAGED_TURN = 0.5;

const wheel = ref(null);
const highlight = ref(null);
const visit = ref(0);
const page = ref(0);
const radial = useTemplateRef("radial");
let openedBy = "";

const pages = computed(() => (wheel.value ? countPages(wheel.value.items) : 1));
const paged = computed(() => pages.value > 1);
const shown = computed(() => (wheel.value ? layPage(wheel.value.items, page.value, t.value) : []));
const caption = computed(() => (paged.value ? fill(t.value.page, { page: page.value + 1, pages: pages.value }) : ""));

const findItem = (id) => wheel.value?.items.find((item) => item.id === id);

async function open(view, key) {
  wheel.value = view;
  openedBy = key.toLowerCase();
  highlight.value = null;
  page.value = 0;
  visit.value++;
  await nextTick();
  radial.value.focus();
}

function turn(step) {
  if (paged.value) page.value = turnPage(page.value, step, pages.value);
}

function choose(id) {
  if (id === NEXT) return turn(1);
  if (id === PREVIOUS) return turn(-1);

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
  const answering = shown.value.find((item, index) => String(item.key ?? index + 1).toLowerCase() === key);
  if (key === "escape" || key === openedBy) {
    event.preventDefault();
    close();
  } else if (paged.value && key in PAGE_KEYS) {
    event.preventDefault();
    turn(PAGE_KEYS[key]);
  } else if (answering) {
    event.preventDefault();
    choose(answering.id);
  }
}

function onWheel(event) {
  if (!paged.value || event.deltaY === 0) return;
  event.preventDefault();
  turn(Math.sign(event.deltaY));
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
  <div v-if="wheel" class="radial" @click.self="close" @contextmenu.prevent="close" @wheel="onWheel">
    <RpRadial
      ref="radial"
      :key="visit"
      v-model="highlight"
      :items="shown"
      :title="wheel.title"
      :caption="caption"
      :turn="paged ? PAGED_TURN : 0"
      :label="t.wheel"
      @select="choose"
    />
    <p class="radial__hint">{{ paged ? t.hint_paged : t.hint }}</p>
    <RpButton class="radial__close" @click="close">{{ t.close }}</RpButton>
  </div>
</template>
