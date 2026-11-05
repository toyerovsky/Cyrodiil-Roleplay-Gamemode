<script setup>
import { computed, useId, useTemplateRef } from "vue";
import { RpButton } from "../rp/index.js";
import { fill } from "../shared/locale.js";
import { TRAIT_MAX, TRAIT_MIN } from "./card.js";

// Writing a field: a line, a long text (Enter a new line, Ctrl+Enter saves) or a trait's number, which stays for good.
const props = defineProps({
  field: { type: Object, required: true },
  kind: { type: String, required: true, validator: (value) => ["text", "long", "number"].includes(value) },
  error: { type: String, default: "" },
  busy: Boolean,
  t: { type: Object, required: true },
});

const emit = defineEmits(["save", "cancel"]);

const text = defineModel({ type: String, default: "" });
const box = useTemplateRef("box");
const id = useId();
const errorId = `${id}-error`;
const TRAIT_DIGITS = String(TRAIT_MAX).length;

const counter = computed(() => fill(props.t.count, { count: text.value.length, max: props.field.max }));

// Digits only: a trait is a number, and the game's keys come without a character to check.
function keepDigits(event) {
  const digits = event.target.value.replace(/\D/g, "").slice(0, TRAIT_DIGITS);
  if (digits !== event.target.value) event.target.value = digits;
  text.value = digits;
}

defineExpose({
  focus() {
    const element = box.value;
    if (!element) return;
    element.focus();
    element.setSelectionRange?.(element.value.length, element.value.length);
  },
});
</script>

<template>
  <div class="lore-edit" :class="`lore-edit--${kind}`">
    <label :for="id" class="lore-edit__label">{{ kind === "number" ? fill(t["trait.range"], { min: TRAIT_MIN, most: TRAIT_MAX }) : t.your_words }}</label>
    <textarea
      v-if="kind === 'long'"
      :id="id"
      ref="box"
      v-model="text"
      class="lore-edit__box lore-edit__box--long"
      :maxlength="field.max"
      spellcheck="false"
      :disabled="busy"
      :aria-invalid="error ? 'true' : undefined"
      :aria-describedby="error ? errorId : undefined"
    />
    <input
      v-else-if="kind === 'number'"
      :id="id"
      ref="box"
      :value="text"
      class="lore-edit__box lore-edit__box--number"
      type="text"
      inputmode="numeric"
      autocomplete="off"
      spellcheck="false"
      :maxlength="TRAIT_DIGITS"
      :placeholder="fill(t['trait.range'], { min: TRAIT_MIN, most: TRAIT_MAX })"
      :disabled="busy"
      :aria-invalid="error ? 'true' : undefined"
      :aria-describedby="error ? errorId : undefined"
      @input="keepDigits"
    >
    <input
      v-else
      :id="id"
      ref="box"
      v-model="text"
      class="lore-edit__box"
      type="text"
      autocomplete="off"
      spellcheck="false"
      :maxlength="field.max"
      :disabled="busy"
      :aria-invalid="error ? 'true' : undefined"
      :aria-describedby="error ? errorId : undefined"
    >
    <p v-if="error" :id="errorId" class="lore-edit__error" role="alert">{{ error }}</p>
    <p v-if="kind === 'number'" class="lore-detail__warn">{{ t["trait.apply_note"] }}</p>
    <p v-else class="lore-edit__count">{{ counter }}</p>
    <div class="lore-detail__actions">
      <RpButton variant="primary" :disabled="busy" data-action="save" @click="emit('save')">
        {{ kind === "number" ? t["trait.apply"] : t.save }}
      </RpButton>
      <RpButton :disabled="busy" data-action="cancel" @click="emit('cancel')">{{ t.cancel }}</RpButton>
    </div>
  </div>
</template>
