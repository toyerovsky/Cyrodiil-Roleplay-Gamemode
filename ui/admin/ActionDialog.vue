<script setup>
import { computed, onMounted, reactive, ref, useId, useTemplateRef } from "vue";
import { RpButton, RpChip, RpField } from "../rp/index.js";
import { CHAT_MAX_LENGTH, MAX_MINUTES, MOST_ITEM_PIECES, REASON_MIN_LENGTH } from "../shared/limits.js";
import { fill } from "../shared/locale.js";
import { ACTIONS } from "./actions.js";

// Every field is checked as the server would, so a refusal names the field. Esc (the panel's) and Cancel drop it.
const props = defineProps({
  dialog: { type: Object, required: true },
  t: { type: Object, required: true },
});

const emit = defineEmits(["run", "cancel"]);

const WHOLE = /^-?\d+$/;
// The lengths a ban or a mute most often has, 0 for one without an end.
const MINUTE_PRESETS = [15, 60, 1440, 10080, 0];
// The roles StaffRoleSeeds.cs makes, from the least.
const ROLES = ["moderator", "admin", "owner"];
const NO_ROLES = "-";
const TEXT_FIELDS = ["reason", "roles", "text"];

const fields = ACTIONS[props.dialog.action].fields;
const current = props.dialog.values.current ?? [];
const startValue = (field) => {
  if (field === "count") return "1";
  if (field === "roles") return current.length ? current.join(" ") : NO_ROLES;
  return "";
};
const values = reactive(Object.fromEntries(fields.map((field) => [field, startValue(field)])));
const problem = ref(null);
const inputs = useTemplateRef("inputs");
const titleId = useId();

const title = computed(() => fill(props.t["dialog.title"], { action: props.t[`action.${props.dialog.action}`], name: props.dialog.name }));
const most = computed(() => Math.min(MOST_ITEM_PIECES, props.dialog.values.most ?? MOST_ITEM_PIECES));

function check() {
  const readNumber = (field) => (WHOLE.test(values[field].trim()) ? Number(values[field].trim()) : NaN);
  if ("minutes" in values && !(readNumber("minutes") >= 0 && readNumber("minutes") <= MAX_MINUTES))
    return { field: "minutes", text: fill(props.t["dialog.minutes_range"], { max: MAX_MINUTES }) };
  if ("reason" in values && values.reason.trim().length < REASON_MIN_LENGTH)
    return { field: "reason", text: fill(props.t["dialog.reason_short"], { min: REASON_MIN_LENGTH }) };
  if ("amount" in values && !(readNumber("amount") !== 0 && Number.isFinite(readNumber("amount"))))
    return { field: "amount", text: props.t["dialog.amount_zero"] };
  if ("roles" in values && values.roles.trim().length === 0) return { field: "roles", text: props.t["dialog.roles_empty"] };
  if ("count" in values && !(readNumber("count") >= 1 && readNumber("count") <= most.value))
    return { field: "count", text: fill(props.t["dialog.count_range"], { max: most.value }) };
  if ("health" in values && !(readNumber("health") >= 1 && readNumber("health") <= most.value))
    return { field: "health", text: fill(props.t["dialog.health_range"], { max: most.value }) };
  if ("text" in values && values.text.trim().length === 0) return { field: "text", text: props.t["dialog.text_empty"] };
  return null;
}

function submit() {
  problem.value = check();
  if (problem.value) return;
  const filled = Object.fromEntries(Object.entries(values).map(([field, value]) => [field, value.trim().replace(/\s+/g, " ")]));
  emit("run", { ...props.dialog.values, ...filled });
}

function presetLabel(minutes) {
  return props.t[`dialog.preset.${minutes}`];
}

onMounted(() => inputs.value?.[0]?.focus());
</script>

<template>
  <div class="admin-dialog">
    <form class="admin-dialog__box" role="dialog" aria-modal="true" :aria-labelledby="titleId" novalidate @submit.prevent="submit">
      <h2 :id="titleId" class="admin-dialog__title">{{ title }}</h2>
      <template v-for="field in fields" :key="field">
        <RpField
          ref="inputs"
          v-model="values[field]"
          :label="fill(t[`field.${field}`], { max: most })"
          :invalid="problem?.field === field"
          :inputmode="TEXT_FIELDS.includes(field) ? 'text' : 'numeric'"
          :maxlength="field === 'text' ? CHAT_MAX_LENGTH : undefined"
          :data-field="field"
        />
        <div v-if="field === 'minutes'" class="admin-chips admin-dialog__presets" role="group" :aria-label="t['dialog.presets']">
          <RpChip v-for="minutes in MINUTE_PRESETS" :key="minutes" :pressed="values.minutes === String(minutes)" @click="values.minutes = String(minutes)">
            {{ presetLabel(minutes) }}
          </RpChip>
        </div>
        <p v-if="field === 'roles'" class="admin__note">{{ fill(t["dialog.roles_known"], { roles: ROLES.join(", "), none: NO_ROLES }) }}</p>
      </template>
      <p v-if="problem" class="admin-dialog__problem" role="alert">{{ problem.text }}</p>
      <div class="admin-dialog__buttons">
        <RpButton @click="emit('cancel')">{{ t["dialog.cancel"] }}</RpButton>
        <RpButton type="submit" variant="primary">{{ t["dialog.run"] }}</RpButton>
      </div>
    </form>
  </div>
</template>
