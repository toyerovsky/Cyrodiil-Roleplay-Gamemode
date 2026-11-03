<script setup>
import { computed, nextTick, onMounted, ref, useId, useTemplateRef } from "vue";
import seal from "../rp/art/seal.webp";
import crest from "../rp/art/crest.webp";
import { RpButton, RpField, RpIlluminated, RpPanel } from "../rp/index.js";
import { NAME_MAX_LENGTH, NAME_MIN_LENGTH } from "../shared/limits.js";
import { fill, usePageTexts } from "../shared/locale.js";
import { isComposing, isHeldPress } from "../shared/press.js";

const props = defineProps({ bridge: { type: Object, required: true } });

const { t } = usePageTexts(props.bridge, "creator");

const shown = ref(false);
const firstName = ref("");
const lastName = ref("");
const busy = ref(false);
const problem = ref("");
const firstField = useTemplateRef("firstField");
const lastField = useTemplateRef("lastField");
const hintId = useId();
const problemId = useId();

const canSubmit = computed(() => !busy.value && firstName.value.trim() !== "" && lastName.value.trim() !== "");
const hint = computed(() => fill(t.value.hint, { min: NAME_MIN_LENGTH, max: NAME_MAX_LENGTH }));

function submit() {
  if (!canSubmit.value) return;
  busy.value = true;
  problem.value = "";
  props.bridge.submitName(firstName.value.trim(), lastName.value.trim());
}

// Enter in the first field moves on rather than sending half a name.
function onFirstEnter(event) {
  if (isComposing(event) || lastName.value.trim()) return;
  event.preventDefault();
  lastField.value.focus();
}

function onFormKeydown(event) {
  if (event.key === "Enter" && isHeldPress(event)) event.preventDefault();
}

onMounted(() => {
  // Shown means the game has handed the page the keyboard, which is the moment a field can take it.
  props.bridge.onShown(async (value) => {
    shown.value = value;
    if (!value) return;
    busy.value = false;
    await nextTick();
    (firstName.value.trim() ? lastField : firstField).value.focus();
  });

  // A refused name is typed again from the start.
  props.bridge.onAnswer(async (text) => {
    busy.value = false;
    problem.value = text;
    if (!text) return;
    await nextTick();
    firstField.value.focus();
    firstField.value.select();
  });

  props.bridge.signalReady();
});
</script>

<template>
  <div class="creator" :class="{ 'creator--shown': shown }">
    <div class="creator__document">
      <img class="creator__crest" :src="crest" alt="">
      <RpPanel class="creator__panel" variant="letter" :title="t.title">
        <RpIlluminated class="creator__lead" :text="t.lead" />

        <form class="creator__form" autocomplete="off" novalidate @submit.prevent="submit" @keydown="onFormKeydown">
          <RpField
            ref="firstField"
            v-model="firstName"
            :label="t.first_name"
            :invalid="!!problem"
            :maxlength="NAME_MAX_LENGTH"
            :placeholder="t.first_placeholder"
            :aria-describedby="`${hintId} ${problemId}`"
            @keydown.enter="onFirstEnter"
          />
          <RpField
            ref="lastField"
            v-model="lastName"
            :label="t.last_name"
            :invalid="!!problem"
            :maxlength="NAME_MAX_LENGTH"
            :placeholder="t.last_placeholder"
            :aria-describedby="`${hintId} ${problemId}`"
          />

          <p :id="hintId" class="creator__hint">{{ hint }}</p>
          <p :id="problemId" class="creator__problem" role="alert">{{ problem }}</p>

          <RpButton class="creator__submit" type="submit" variant="primary" :disabled="!canSubmit" :aria-busy="busy">
            {{ busy ? t.checking : t.submit }}
          </RpButton>
        </form>

        <p class="creator__note">{{ t.note }}</p>
      </RpPanel>
      <img class="creator__seal" :class="{ 'creator__seal--pressed': canSubmit || busy }" :src="seal" alt="">
    </div>
  </div>
</template>
