<script setup>
import { computed, ref, watch } from "vue";
import CardView from "../lore/CardView.vue";
import { RpButton } from "../rp/index.js";
import { pickTexts } from "../shared/locale.js";

// A player's lore card for the staff, every field whoever may see it, read on request. Clear empties one field through
// /alore, asked twice so a slip of the mouse clears nothing.
const props = defineProps({
  lore: { type: Object, default: null },
  asked: Boolean,
  t: { type: Object, required: true },
  locale: { type: String, required: true },
  may: { type: Function, required: true },
  can: { type: Function, required: true },
});

const emit = defineEmits(["read", "clear"]);

const CONFIRM_MS = 2500;
const words = computed(() => pickTexts(props.locale, "ui.lore."));
const confirming = ref(null);
let confirmTimer = 0;

watch(
  () => props.lore,
  () => (confirming.value = null),
);

function clear(field) {
  if (confirming.value === field.id) {
    confirming.value = null;
    clearTimeout(confirmTimer);
    emit("clear", field.id);
    return;
  }
  confirming.value = field.id;
  clearTimeout(confirmTimer);
  confirmTimer = setTimeout(() => (confirming.value = null), CONFIRM_MS);
}
</script>

<template>
  <section class="admin-lore" :aria-label="t['card.lore']">
    <h3 class="admin__heading">{{ t["card.lore"] }}</h3>
    <RpButton v-if="!asked" data-action="lore" @click="emit('read')">{{ t["card.lore_read"] }}</RpButton>
    <p v-else-if="!lore" class="admin__note" role="status">{{ t.loading }}</p>
    <p v-else-if="!lore.fields?.length" class="admin__note">{{ t["card.lore_none"] }}</p>
    <CardView v-else class="admin-lore__card" :view="lore" :words="words" :heading-level="4">
      <template #field="{ field }">
        <RpButton
          v-if="may('loreClear')"
          class="admin-lore__clear"
          :disabled="!can('loreClear')"
          :data-clear="field.id"
          @click="clear(field)"
        >
          {{ confirming === field.id ? t["card.lore_clear_again"] : t["action.loreClear"] }}
        </RpButton>
      </template>
    </CardView>
  </section>
</template>
