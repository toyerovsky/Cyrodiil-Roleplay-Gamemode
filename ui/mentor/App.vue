<script setup>
import { computed, nextTick, onMounted, onUnmounted, ref, useId, useTemplateRef } from "vue";
import { RpButton, RpIcon, RpPanel } from "../rp/index.js";
import { fill, usePageTexts } from "../shared/locale.js";
import { isComposing, isHeldPress } from "../shared/press.js";
import { pictureOf } from "./pictures.js";

const props = defineProps({ bridge: { type: Object, required: true } });

const { locale, t } = usePageTexts(props.bridge, "mentor");

const SECOND_MS = 1000;
const MINUTE_MS = 60 * SECOND_MS;
const HOUR_MS = 60 * MINUTE_MS;

// The mentor's window as the server last sent it. Null while no mentor's window is open.
const mentor = ref(null);
// The specialization that would take the place of the one chosen, while the player is asked to say so.
const replacing = ref(null);
const answer = ref(null);
const busy = ref(false);
const panel = useTemplateRef("panel");
// The clock the wait for a change counts down by, moved on every second while a wait shows.
const now = ref(Date.now());
let ticker = null;
const confirmId = useId();

const hasChosen = computed(() => Boolean(mentor.value?.chosen));
const status = computed(() => (busy.value ? t.value.waiting : (answer.value?.text ?? t.value.hint)));
const title = computed(() => (mentor.value?.path ? t.value[`title.${mentor.value.path}`] : t.value["title.registrar"]));
const msLeft = computed(() => (mentor.value?.nextChoiceAt ? Math.max(0, mentor.value.nextChoiceAt - now.value) : 0));
const isWaiting = computed(() => msLeft.value > 0);
const isDear = computed(() => (mentor.value?.price ?? 0) > (mentor.value?.gold ?? 0));

const nameOf = (specialization) => nameOfId(specialization.id);
const nameOfId = (id) => t.value[`name.${id}`] ?? id;
const statOf = (stat) => t.value[`stat.${stat}`] ?? stat;
const listStats = (stats) => stats.map(statOf).join(", ");
const showGold = (count) => fill(t.value.gold, { count: count.toLocaleString(locale.value) });
const canChoose = (specialization) => !busy.value && !replacing.value && !specialization.chosen && !isWaiting.value && !isDear.value;

const nextChoice = computed(() => {
  if (!mentor.value) return "";
  if (isWaiting.value) return describeWait(msLeft.value);
  return mentor.value.price === 0 ? t.value.next_free : showGold(mentor.value.price);
});

// In its largest whole unit, rounded up, as the server says it.
function describeWait(ms) {
  if (ms >= HOUR_MS) return fill(t.value.next_wait_hours, { count: Math.ceil(ms / HOUR_MS) });
  if (ms >= MINUTE_MS) return fill(t.value.next_wait_minutes, { count: Math.ceil(ms / MINUTE_MS) });
  return fill(t.value.next_wait_seconds, { count: Math.max(1, Math.ceil(ms / SECOND_MS)) });
}

function stopTicker() {
  clearInterval(ticker);
  ticker = null;
}

// Ticks only while a wait shows, and stops once it is over and the choices open.
function followWait() {
  now.value = Date.now();
  if (!isWaiting.value) {
    stopTicker();
    return;
  }

  ticker ??= setInterval(() => {
    now.value = Date.now();
    if (!isWaiting.value) {
      stopTicker();
      keepFocus();
    }
  }, SECOND_MS);
}

function readAction(specialization) {
  if (specialization.chosen) return t.value.yours;
  if (isWaiting.value) return t.value.wait;
  return isDear.value ? t.value.too_poor : t.value.choose;
}

// The first specialization there is to choose, or Leave when there is none.
function focusFirst() {
  const root = panel.value?.$el;
  (root?.querySelector("[data-specialization] button:not(:disabled)") ?? root?.querySelector('[data-action="close"]'))?.focus();
}

// A button that went disabled under the focus hands it on, so the keys keep working.
async function keepFocus() {
  await nextTick();
  const root = panel.value?.$el;
  const focused = document.activeElement;
  if (!root?.contains(focused) || focused.disabled) focusFirst();
}

function pick(specialization) {
  if (!canChoose(specialization)) return;
  if (!hasChosen.value) {
    choose(specialization);
    return;
  }

  replacing.value = specialization;
  nextTick(() => panel.value?.$el.querySelector('[data-action="confirm"]')?.focus());
}

function choose(specialization) {
  busy.value = true;
  answer.value = null;
  replacing.value = null;
  props.bridge.choose(specialization.id);
}

function cancel() {
  const id = replacing.value?.id;
  replacing.value = null;
  nextTick(() => panel.value?.$el.querySelector(`[data-specialization="${id}"] button`)?.focus());
}

async function onState(view) {
  const opening = !mentor.value;
  mentor.value = view;
  followWait();
  if (replacing.value && !view.offered.some((each) => each.id === replacing.value.id && !each.chosen)) replacing.value = null;
  if (!opening) {
    await keepFocus();
    return;
  }

  replacing.value = null;
  answer.value = null;
  busy.value = false;
  await nextTick();
  focusFirst();
}

function onAnswer(text, done) {
  busy.value = false;
  answer.value = { text, done };
  keepFocus();
}

function close() {
  stopTicker();
  mentor.value = null;
  replacing.value = null;
  answer.value = null;
  busy.value = false;
}

function leave() {
  if (mentor.value) props.bridge.close();
}

function onKeydown(event) {
  if (!mentor.value || isComposing(event)) return;
  if (isHeldPress(event)) {
    event.preventDefault();
    return;
  }

  if (event.key !== "Escape") return;
  event.preventDefault();
  if (replacing.value) cancel();
  else leave();
}

onMounted(() => {
  props.bridge.onState(onState);
  props.bridge.onAnswer(onAnswer);
  props.bridge.onClosed(close);
  window.addEventListener("keydown", onKeydown);
  props.bridge.signalReady();
});

onUnmounted(() => {
  stopTicker();
  window.removeEventListener("keydown", onKeydown);
});
</script>

<template>
  <div v-if="mentor" class="mentor">
    <RpPanel ref="panel" class="mentor__panel" variant="board" :title="title">
      <p class="mentor__lead">
        {{ fill(t.lead, { trained: mentor.caps.trainedSkill, limited: mentor.caps.limitedSkill }) }}
      </p>

      <dl class="mentor__sums">
        <div class="mentor__sum" data-sum="specialization">
          <dt><RpIcon name="crown" class="mentor__mark" />{{ t.yours_now }}</dt>
          <dd>{{ hasChosen ? nameOfId(mentor.chosen) : t.none }}</dd>
        </div>
        <div class="mentor__sum" data-sum="gold">
          <dt><RpIcon name="coins" class="mentor__mark" />{{ t.purse }}</dt>
          <dd>{{ showGold(mentor.gold) }}</dd>
        </div>
        <div class="mentor__sum" data-sum="next">
          <dt><RpIcon name="scales" class="mentor__mark" />{{ hasChosen ? t.next_change : t.next_first }}</dt>
          <dd>{{ nextChoice }}</dd>
        </div>
      </dl>

      <p v-if="!hasChosen" class="mentor__note">{{ fill(t.unchosen_note, { cap: mentor.caps.unchosenSkill }) }}</p>
      <p v-else class="mentor__note">{{ t.change_note }}</p>

      <ul class="mentor__list" :aria-label="t.offered">
        <li
          v-for="specialization in mentor.offered"
          :key="specialization.id"
          class="mentor__item"
          :class="{ 'mentor__item--chosen': specialization.chosen }"
          :data-specialization="specialization.id"
        >
          <img :src="pictureOf(specialization.id)" alt="" class="mentor__picture" />
          <div class="mentor__body">
            <span class="mentor__name">{{ nameOf(specialization) }}</span>
            <span class="mentor__path">{{ t[`path.${specialization.path}`] }}</span>
            <p class="mentor__about">{{ t[`about.${specialization.id}`] }}</p>
            <p class="mentor__stats" data-stats="mastered">
              <strong>{{ t.mastered }}</strong> {{ listStats(specialization.mastered) }}
            </p>
            <p class="mentor__stats" data-stats="trained">
              <strong>{{ fill(t.trained, { cap: mentor.caps.trainedSkill }) }}</strong> {{ listStats(specialization.trained) }}
            </p>
            <p class="mentor__stats" data-stats="majors">
              <strong>{{ t.majors }}</strong> {{ listStats(specialization.majors) }}
            </p>
          </div>
          <RpButton
            class="mentor__choose"
            :variant="canChoose(specialization) ? 'primary' : 'default'"
            :disabled="!canChoose(specialization)"
            :aria-busy="busy"
            :aria-label="fill(t.action, { action: readAction(specialization), specialization: nameOf(specialization) })"
            @click="pick(specialization)"
          >
            {{ readAction(specialization) }}
          </RpButton>
        </li>
      </ul>

      <p class="mentor__rest">
        {{ fill(t.rest, { limited: mentor.caps.limitedSkill, attribute: mentor.caps.limitedAttribute }) }}
      </p>

      <section v-if="replacing" class="mentor__confirm" role="alertdialog" :aria-labelledby="confirmId">
        <p :id="confirmId" class="mentor__question">
          {{ fill(t.confirm, { specialization: nameOf(replacing), price: mentor.price.toLocaleString(locale) }) }}
        </p>
        <div class="mentor__acts">
          <RpButton variant="primary" data-action="confirm" @click="choose(replacing)">{{ t.confirm_yes }}</RpButton>
          <RpButton data-action="cancel" @click="cancel">{{ t.confirm_no }}</RpButton>
        </div>
      </section>

      <p class="mentor__status" :class="answer && !busy && (answer.done ? 'mentor__status--done' : 'mentor__status--refused')" role="status">
        {{ status }}
      </p>

      <div class="mentor__footer">
        <RpButton data-action="close" @click="leave">{{ t.close }}</RpButton>
      </div>
    </RpPanel>
  </div>
</template>
