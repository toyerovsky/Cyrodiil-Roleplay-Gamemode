<script setup>
import { computed, nextTick, onMounted, onUnmounted, ref, useId, useTemplateRef } from "vue";
import { RpButton, RpPanel } from "../rp/index.js";
import { fill, usePageTexts } from "../shared/locale.js";
import { isComposing, isHeldPress } from "../shared/press.js";
import { formatAge, formatSpan } from "./format.js";

const props = defineProps({ bridge: { type: Object, required: true } });

const SECOND_MS = 1000;
const MINUTE_SECONDS = 60;

const { locale, t } = usePageTexts(props.bridge, "notices");

// The board as the server last sent it, null while no board's window is open.
const board = ref(null);
const text = ref("");
const problem = ref(null);
const answer = ref(null);
// What the page waits for the server to answer: "posting", "taking_down" or null.
const pending = ref(null);
// When the reader may pin again, by this page's clock, and the clock as the countdown last read it.
const readyAt = ref(0);
const now = ref(Date.now());
const field = useTemplateRef("field");
const fieldId = useId();
const problemId = `${fieldId}-problem`;
let ticking = null;

const waitSeconds = computed(() => Math.max(0, Math.ceil((readyAt.value - now.value) / SECOND_MS)));
const atLimit = computed(() => board.value.mine >= board.value.perCharacter);
const canPost = computed(() => !pending.value && !atLimit.value && waitSeconds.value === 0);
const counter = computed(() => fill(t.value.count, { count: text.value.length, max: board.value.maxLength }));
const lead = computed(() => fill(t.value.lead, { days: formatSpan(board.value.expiryDays, "day", locale.value) }));
const status = computed(() => (pending.value ? t.value[pending.value] : (answer.value?.text ?? t.value.hint)));

// The rule that holds the reader back now, or how many they have up.
const rule = computed(() => {
  if (atLimit.value) return t.value.too_many;
  if (waitSeconds.value > 0) {
    const minutes = Math.ceil(waitSeconds.value / MINUTE_SECONDS);
    return fill(t.value.wait, { time: formatSpan(minutes, "minute", locale.value) });
  }
  const mine = fill(t.value.mine, { count: board.value.mine, most: board.value.perCharacter });
  return board.value.notices.length >= board.value.shown ? `${mine} ${t.value.full}` : mine;
});

function post() {
  if (!canPost.value) return;
  const length = text.value.trim().length;
  if (length < board.value.minLength) {
    problem.value = fill(t.value.too_short, { min: board.value.minLength, max: board.value.maxLength });
    field.value.focus();
    return;
  }

  problem.value = null;
  answer.value = null;
  pending.value = "posting";
  props.bridge.post(text.value);
}

function takeDown(notice) {
  if (pending.value) return;
  answer.value = null;
  pending.value = "taking_down";
  props.bridge.takeDown(notice.id);
}

function tick() {
  now.value = Date.now();
  if (waitSeconds.value === 0) stopTicking();
}

function stopTicking() {
  clearInterval(ticking);
  ticking = null;
}

async function onState(view) {
  const opening = !board.value;
  board.value = view;
  now.value = Date.now();
  readyAt.value = now.value + view.waitSeconds * SECOND_MS;
  if (view.waitSeconds > 0 && !ticking) ticking = setInterval(tick, SECOND_MS);
  if (!opening) return;

  text.value = "";
  problem.value = null;
  answer.value = null;
  pending.value = null;
  await nextTick();
  field.value.focus();
}

function onAnswer(said, done) {
  if (done && pending.value === "posting") text.value = "";
  pending.value = null;
  answer.value = { text: said, done };
}

function close() {
  board.value = null;
  answer.value = null;
  pending.value = null;
  stopTicking();
}

function leave() {
  if (board.value) props.bridge.close();
}

function onKeydown(event) {
  if (!board.value || isComposing(event)) return;
  if (isHeldPress(event)) {
    event.preventDefault();
    return;
  }

  if (event.key === "Escape") {
    event.preventDefault();
    leave();
  } else if (event.key === "Enter" && event.ctrlKey && event.target === field.value) {
    event.preventDefault();
    post();
  }
}

function onTyped() {
  problem.value = null;
}

onMounted(() => {
  props.bridge.onState(onState);
  props.bridge.onAnswer(onAnswer);
  props.bridge.onClosed(close);
  window.addEventListener("keydown", onKeydown);
  props.bridge.signalReady();
});

onUnmounted(() => {
  window.removeEventListener("keydown", onKeydown);
  stopTicking();
});
</script>

<template>
  <div v-if="board" class="notices">
    <RpPanel class="notices__panel" :title="t.title">
      <p class="notices__lead">{{ lead }}</p>

      <ul v-if="board.notices.length" class="notices__list" :aria-label="t.list">
        <li v-for="notice in board.notices" :key="notice.id" class="notice" :class="{ 'notice--mine': notice.mine }" :data-notice="notice.id">
          <p class="notice__text">{{ notice.text }}</p>
          <div class="notice__foot">
            <span class="notice__age">{{ fill(t.pinned, { age: formatAge(notice.ageMinutes, locale) }) }}</span>
            <template v-if="notice.mine">
              <span class="notice__yours">{{ t.yours }}</span>
              <RpButton class="notice__take" :disabled="!!pending" data-action="take-down" @click="takeDown(notice)">{{ t.take_down }}</RpButton>
            </template>
          </div>
        </li>
      </ul>
      <p v-else class="notices__empty">{{ t.empty }}</p>

      <form class="notices__form" novalidate @submit.prevent="post">
        <div class="notices__heading">
          <label :for="fieldId" class="notices__label">{{ t.write }}</label>
          <span class="notices__count" data-count>{{ counter }}</span>
        </div>
        <textarea
          :id="fieldId"
          ref="field"
          v-model="text"
          class="notices__field"
          :maxlength="board.maxLength"
          spellcheck="false"
          :aria-invalid="problem ? 'true' : undefined"
          :aria-describedby="problem ? problemId : undefined"
          data-field="notice"
          @input="onTyped"
        />
        <p v-if="problem" :id="problemId" class="notices__problem" role="alert">{{ problem }}</p>
        <div class="notices__acts">
          <p class="notices__rule" :class="{ 'notices__rule--held': atLimit || waitSeconds > 0 }" data-rule>{{ rule }}</p>
          <RpButton variant="primary" type="submit" :disabled="!canPost" :aria-busy="pending === 'posting'" data-action="post">{{ t.post }}</RpButton>
        </div>
      </form>

      <p class="notices__status" :class="answer && !pending && (answer.done ? 'notices__status--done' : 'notices__status--refused')" role="status">
        {{ status }}
      </p>

      <div class="notices__footer">
        <RpButton data-action="close" @click="leave">{{ t.close }}</RpButton>
      </div>
    </RpPanel>
  </div>
</template>
