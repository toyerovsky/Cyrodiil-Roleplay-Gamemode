<script>
// A tab's id, by which the panel it controls is named (aria-labelledby).
export const makeTabId = (panel, tab) => `${panel}-tab-${tab}`;
</script>

<script setup>
import { nextTick, useTemplateRef } from "vue";
import { isComposing, isHeldPress, readCode } from "../shared/press.js";

// A column of tabs under the medallions of the game's own menus. Large is the medallions' own size at 1080p, small for
// a page that needs the room.
const props = defineProps({
  tabs: { type: Array, required: true },
  label: { type: String, required: true },
  controls: { type: String, required: true },
  size: { type: String, default: "large", validator: (value) => ["large", "small"].includes(value) },
});

const selected = defineModel({ type: String, required: true });

const STEPS = { KeyQ: -1, ArrowUp: -1, ArrowLeft: -1, KeyE: 1, ArrowDown: 1, ArrowRight: 1 };

const buttons = useTemplateRef("buttons");

async function focus() {
  await nextTick();
  buttons.value?.find((button) => button.dataset.tab === selected.value)?.focus();
}

function turn(by) {
  const at = props.tabs.findIndex((each) => each.id === selected.value);
  selected.value = props.tabs[(at + by + props.tabs.length) % props.tabs.length].id;
  focus();
}

// Q and E, read by the key's place, and the arrows go round the tabs. The tab list takes them while a tab has the focus,
// a page that turns the tabs from anywhere hands its keys on. Says whether the key was one of the tabs'.
function takeKey(event) {
  const step = STEPS[readCode(event)];
  if (!step || isComposing(event)) return false;
  event.preventDefault();
  if (!isHeldPress(event)) turn(step);
  return true;
}

defineExpose({ focus, takeKey });
</script>

<template>
  <div class="rp-tabs" :class="`rp-tabs--${size}`" role="tablist" aria-orientation="vertical" :aria-label="label" @keydown="takeKey">
    <button
      v-for="each in tabs"
      :id="makeTabId(controls, each.id)"
      ref="buttons"
      :key="each.id"
      type="button"
      role="tab"
      class="rp-tabs__tab"
      :class="{ 'rp-tabs__tab--on': each.id === selected }"
      :data-tab="each.id"
      :aria-selected="each.id === selected"
      :aria-controls="controls"
      :tabindex="each.id === selected ? 0 : -1"
      @click="selected = each.id"
    >
      <img class="rp-tabs__medallion" :src="each.medallion" alt="">
      <span class="rp-tabs__label">{{ each.label }}</span>
    </button>
  </div>
</template>
