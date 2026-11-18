<script setup>
import { computed, ref, watch } from "vue";
import { RpButton, RpChip } from "../rp/index.js";
import { pickTexts } from "../shared/locale.js";

// A character's specialization and every attribute and skill with its value and cap, for a fix after a fault or a cheat.
// A chip sets the specialization through /aspec, asked twice so a slip of the mouse changes nothing; the rows set a cap
// (/acap) or a value (/astat) through a dialog, and Clear gives the specialization's cap back.
const props = defineProps({
  card: { type: Object, required: true },
  t: { type: Object, required: true },
  locale: { type: String, required: true },
  may: { type: Function, required: true },
  can: { type: Function, required: true },
});

const emit = defineEmits(["act"]);

const NONE = "none";
const SPECIALIZATIONS = ["Knight", "Barbarian", "Ranger", "Wizard", "Alchemist", "Illusionist", "Healer", "Smith", "Thief"];
const CONFIRM_MS = 2500;

const names = computed(() => pickTexts(props.locale, "ui.mentor."));
const current = computed(() => props.card.specializationId ?? NONE);
const confirming = ref(null);
let confirmTimer = 0;

const nameOf = (id) => (id === NONE ? props.t["card.spec_none"] : (names.value[`name.${id}`] ?? id));

watch(
  () => props.card,
  () => (confirming.value = null),
);

function pick(id) {
  if (id === current.value || !props.can("spec")) return;
  if (confirming.value !== id) {
    confirming.value = id;
    clearTimeout(confirmTimer);
    confirmTimer = setTimeout(() => (confirming.value = null), CONFIRM_MS);
    return;
  }

  confirming.value = null;
  clearTimeout(confirmTimer);
  emit("act", "spec", { specialization: id });
}

const show = (number) => (number === null ? "-" : String(number));
</script>

<template>
  <section class="admin-spec" :aria-label="t['card.spec']">
    <h3 class="admin__heading">{{ t["card.spec"] }}</h3>
    <p v-if="!card.stats" class="admin__note">{{ t["card.spec_unread"] }}</p>
    <template v-else>
      <div v-if="may('spec')" class="admin-chips admin-spec__picks" role="group" :aria-label="t['card.spec_pick']">
        <RpChip
          v-for="id in [...SPECIALIZATIONS, NONE]"
          :key="id"
          :pressed="current === id"
          :disabled="!can('spec')"
          :data-spec="id"
          @click="pick(id)"
        >
          {{ confirming === id ? t["card.spec_again"] : nameOf(id) }}
        </RpChip>
      </div>

      <table class="admin-spec__stats">
        <thead>
          <tr>
            <th scope="col">{{ t["card.stat"] }}</th>
            <th scope="col">{{ t["card.stat_value"] }}</th>
            <th scope="col">{{ t["card.stat_cap"] }}</th>
            <th scope="col"><span class="admin__hidden">{{ t["card.actions"] }}</span></th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="stat in card.stats" :key="stat.id" :data-stat="stat.id">
            <th scope="row">{{ stat.name }}</th>
            <td>{{ show(stat.value) }}</td>
            <td :class="{ 'admin-spec__cap--set': stat.overridden }">
              {{ stat.cap === null ? t["card.stat_uncapped"] : stat.cap }}
              <span v-if="stat.overridden" class="admin-spec__mark">{{ t["card.stat_overridden"] }}</span>
            </td>
            <td>
              <div class="admin-spec__acts">
                <RpButton
                  v-if="may('statValue')"
                  class="admin-spec__button"
                  :disabled="!can('statValue') || stat.value === null"
                  data-act="value"
                  @click="emit('act', 'statValue', { stat: stat.id, statName: stat.name, most: stat.cap ?? 100 })"
                >
                  {{ t["card.stat_set_value"] }}
                </RpButton>
                <RpButton
                  v-if="may('cap')"
                  class="admin-spec__button"
                  :disabled="!can('cap')"
                  data-act="cap"
                  @click="emit('act', 'cap', { stat: stat.id, statName: stat.name })"
                >
                  {{ t["card.stat_set_cap"] }}
                </RpButton>
                <RpButton
                  v-if="stat.overridden && may('capClear')"
                  class="admin-spec__button"
                  :disabled="!can('capClear')"
                  data-act="clear"
                  @click="emit('act', 'capClear', { stat: stat.id })"
                >
                  {{ t["card.stat_clear_cap"] }}
                </RpButton>
              </div>
            </td>
          </tr>
        </tbody>
      </table>
    </template>
  </section>
</template>
