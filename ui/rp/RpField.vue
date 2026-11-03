<script setup>
import { useId, useTemplateRef } from "vue";

// The attributes a page gives (maxlength, placeholder) belong to the input, not the wrapper.
defineOptions({ inheritAttrs: false });

defineProps({
  label: { type: String, required: true },
  invalid: Boolean,
});

const value = defineModel({ type: String, default: "" });
const id = useId();
const input = useTemplateRef("input");

defineExpose({
  focus: () => input.value.focus(),
  select: () => input.value.select(),
});
</script>

<template>
  <div class="rp-field">
    <label :for="id" class="rp-field__label">{{ label }}</label>
    <input
      :id="id"
      ref="input"
      v-model="value"
      class="rp-field__input"
      spellcheck="false"
      :aria-invalid="invalid ? 'true' : undefined"
      v-bind="$attrs"
    >
  </div>
</template>
