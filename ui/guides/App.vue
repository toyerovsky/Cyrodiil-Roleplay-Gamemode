<script setup>
import { computed, nextTick, onMounted, onUnmounted, ref, useTemplateRef } from "vue";
import { RpButton } from "../rp/index.js";
import { usePageTexts } from "../shared/locale.js";
import { isComposing, isHeldPress, readCode } from "../shared/press.js";
import { numeral, PICTURES } from "./pictures.js";

// A guide's card over the darkened game, as a film opens: black bands close in, the title comes with a flare across it,
// the steps follow one by one and the picture rises out of the dark. In mode Use, E or Enter lets what waited behind the
// card go on (the shop opens) and Esc only closes it. The welcome and a guide read again from the menu only close.
const props = defineProps({ bridge: { type: Object, required: true } });

// A press this soon after the card came is the hand still on the key that opened the window behind it.
const ARM_MS = 600;
// More steps than this (the welcome's keys) stand closer, under a smaller title.
const LONG_STEPS = 6;
const TITLE_ID = "guide-title";
const MODE = { use: "Use", entry: "Entry", read: "Read" };
// The welcome's lines are keys with a word each, not steps one after another.
const PLAIN = new Set(["Welcome"]);

const { locale, t } = usePageTexts(props.bridge, "guides");

const guide = ref(null);
const visit = ref(0);
const armed = ref(false);
const sent = ref(false);
const primary = useTemplateRef("primary");
let arming = 0;

const goesOn = computed(() => guide.value?.mode === MODE.use);
const reading = computed(() => guide.value?.mode === MODE.read);
const plain = computed(() => PLAIN.has(guide.value?.topic));
const long = computed(() => (guide.value?.steps.length ?? 0) > LONG_STEPS);
const eyebrow = computed(() => {
  if (guide.value?.mode === MODE.entry) return t.value.eyebrow_welcome;
  return reading.value ? t.value.eyebrow_read : t.value.eyebrow;
});

async function onState(view) {
  guide.value = view;
  visit.value++;
  sent.value = false;
  armed.value = false;
  clearTimeout(arming);
  arming = setTimeout(() => (armed.value = true), ARM_MS);
  await nextTick();
  primary.value?.$el.focus();
}

function onClosed() {
  guide.value = null;
  clearTimeout(arming);
}

// One answer a card: the client closes it at the first, and a second would reach whatever opened behind it.
function goOn() {
  if (!guide.value || sent.value) return;
  if (!goesOn.value) {
    close();
    return;
  }
  sent.value = true;
  props.bridge.goOn();
}

function close() {
  if (!guide.value || sent.value) return;
  sent.value = true;
  props.bridge.close();
}

// Heard before the buttons, so Enter on a focused button does not answer twice.
function onKeydown(event) {
  if (!guide.value || isComposing(event)) return;
  const code = readCode(event);
  if (code === "Escape") {
    event.preventDefault();
    if (!event.repeat) close();
    return;
  }
  if (code !== "KeyE" && code !== "Enter") return;
  event.preventDefault();
  if (isHeldPress(event) || !armed.value) return;
  goOn();
}

onMounted(() => {
  props.bridge.onState(onState);
  props.bridge.onClosed(onClosed);
  window.addEventListener("keydown", onKeydown, true);
  props.bridge.signalReady();
});

onUnmounted(() => {
  window.removeEventListener("keydown", onKeydown, true);
  clearTimeout(arming);
});
</script>

<template>
  <div v-if="guide" :key="visit" :class="['guide', { 'guide--long': long }]" :lang="locale" :data-topic="guide.topic">
    <div class="guide__veil" aria-hidden="true"></div>

    <article class="guide__folio" role="dialog" aria-modal="true" :aria-labelledby="TITLE_ID">
      <div class="guide__page">
        <header class="guide__head">
          <p class="guide__eyebrow">{{ eyebrow }}</p>
          <h1 :id="TITLE_ID" class="guide__title">{{ guide.title }}</h1>
          <p class="guide__purpose">{{ guide.purpose }}</p>
          <span class="guide__rule" aria-hidden="true"></span>
        </header>

        <figure class="guide__plate">
          <img class="guide__picture" :src="PICTURES[guide.topic]" alt="">
        </figure>

        <ol :class="['guide__steps', { 'guide__steps--plain': plain }]">
          <li v-for="(step, index) in guide.steps" :key="index" class="guide__step" :style="{ '--order': index }">
            <span v-if="!plain" class="guide__number" aria-hidden="true">{{ numeral(index) }}</span>
            <span v-if="step.keys.length || plain" class="guide__keys">
              <kbd v-for="key in step.keys" :key="key" class="guide__keycap">{{ key }}</kbd>
            </span>
            <span class="guide__text">{{ step.text }}</span>
          </li>
        </ol>

        <section v-if="guide.facts.length" class="guide__facts" :aria-label="t.facts" :style="{ '--after': guide.steps.length }">
          <dl class="guide__figures">
            <div v-for="fact in guide.facts" :key="fact.label" class="guide__fact">
              <dt>{{ fact.label }}</dt>
              <dd>{{ fact.value }}</dd>
            </div>
          </dl>
        </section>
      </div>

      <footer class="guide__actions">
        <p class="guide__again">{{ reading ? "" : t.again }}</p>
        <div class="guide__buttons">
          <template v-if="goesOn">
            <RpButton class="guide__button" data-close @click="close">
              <kbd class="guide__hint">Esc</kbd>{{ t.close }}
            </RpButton>
            <RpButton ref="primary" class="guide__button" variant="primary" data-go-on @click="goOn">
              <kbd class="guide__hint">E</kbd>{{ t.go_on }}
            </RpButton>
          </template>
          <RpButton v-else ref="primary" class="guide__button" variant="primary" data-close @click="close">
            <kbd class="guide__hint">Esc</kbd>{{ reading ? t.back : t.close }}
          </RpButton>
        </div>
      </footer>
    </article>
  </div>
</template>
