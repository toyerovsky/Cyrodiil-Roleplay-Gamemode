<script setup>
import { computed } from "vue";
import d20 from "../rp/art/d20.webp";
import { RpButton, RpChip } from "../rp/index.js";
import { fill } from "../shared/locale.js";
import { PRIVATE, PUBLIC, TRAIT_MAX, TRAIT_MIN, traitState, traitWord, visibilityOf } from "./card.js";
import LoreIcon from "./LoreIcon.vue";

// The field under the cursor: who sees it, what it holds and what can be done with it, every action a button.
const props = defineProps({
  row: { type: Object, required: true },
  value: { type: Object, required: true },
  t: { type: Object, required: true },
  name: { type: String, default: "" },
  namePublic: { type: Boolean, default: false },
  header: { type: String, default: "" },
  raceHint: { type: Object, default: null },
  needs: { type: Object, default: null },
  readOnly: Boolean,
  rolling: Boolean,
  confirmingClear: Boolean,
  busy: Boolean,
});

const emit = defineEmits(["vis", "namevis", "edit", "choose", "clear", "roll", "keep"]);

const LEVELS = [PUBLIC, PRIVATE];

const vis = computed(() => (props.row.name ? (props.namePublic ? PUBLIC : PRIVATE) : visibilityOf(props.row, props.value)));
const trait = computed(() => (props.row.type === "trait" ? traitState(props.value) : null));
const locked = computed(() => !props.row.name && props.row.locked);
const kindLine = computed(() => {
  const field = props.row;
  if (field.name) return props.t["kind_line.name"];
  return fill(props.t[`kind_line.${field.type}`] ?? props.t["kind_line.text"], {
    max: field.max,
    count: field.choices?.length ?? 0,
    group: field.group ?? "",
    min: TRAIT_MIN,
    most: TRAIT_MAX,
  });
});
const editLabel = computed(() => {
  if (props.row.type === "catalog") return props.t.pick;
  if (props.row.type === "choice") return props.t.choose;
  return props.value.empty ? props.t.write : props.t.edit;
});
const showRaceHint = computed(() => props.row.id === "race" && props.value.empty && props.raceHint);
const rollLine = (roll, index) => fill(props.t[index === 0 ? "trait.rolled" : "trait.rerolled"], { dice: roll.dice.join(" + "), total: roll.total });

function pickVisibility(level) {
  if (props.row.name) emit("namevis", level === PUBLIC);
  else emit("vis", level);
}
</script>

<template>
  <div class="lore-detail__field">
    <h2 class="lore-detail__title">
      <LoreIcon v-if="row.type === 'trait'" class="lore-detail__mark" name="die" />
      {{ row.name ? t.name : row.label }}
    </h2>
    <p class="lore-detail__kind">{{ kindLine }}</p>

    <div class="lore-who" role="group" :aria-label="t.who_sees">
      <span class="lore-who__caption">{{ t.who_sees }}</span>
      <template v-if="locked">
        <span class="lore-vis lore-vis--pill" :class="`lore-vis--${vis}`"><LoreIcon :name="vis" />{{ t[`vis.${vis}`] }}</span>
        <small class="lore-who__locked">{{ fill(t.always, { label: row.label, vis: t[`vis.${vis}`] }) }}</small>
      </template>
      <template v-else>
        <RpChip
          v-for="level in LEVELS"
          :key="level"
          class="lore-who__chip"
          :pressed="vis === level"
          :disabled="readOnly"
          :data-vis="level"
          @click="pickVisibility(level)"
        >
          <LoreIcon :name="level" />{{ t[`vis.${level}`] }}
        </RpChip>
      </template>
    </div>
    <p class="lore-detail__help">{{ t[`vis_help.${row.name ? (namePublic ? 'name_public' : 'name_private') : vis}`] }}</p>

    <div class="lore-detail__current">
      <template v-if="row.name">
        <p class="lore-detail__value lore-detail__value--name">{{ name || t.no_name }}</p>
        <p v-if="header" class="lore-detail__seen">{{ fill(t.seen_as, { header }) }}</p>
      </template>

      <template v-else-if="trait">
        <div v-if="rolling" class="lore-trait-big lore-trait-big--rolling" role="status">
          <img class="lore-trait-big__die" :src="d20" alt="">
          <span>{{ t["trait.rolling_dice"] }}</span>
        </div>
        <p v-else-if="trait === 'none'" class="lore-detail__none">{{ t["trait.none"] }}</p>
        <div v-else class="lore-trait-big">
          <b class="lore-trait-big__number">{{ value.number }}</b>
          <div class="lore-trait-big__words">
            <span>{{ fill(t["trait.phrase"], { word: t[`word.${traitWord(value.number)}`], label: row.label.toLocaleLowerCase() }) }}</span>
            <span class="lore-trait__bar" aria-hidden="true"><span :style="{ width: `${value.number}%` }" /></span>
          </div>
        </div>
        <ul v-if="value.rolls?.length && !rolling" class="lore-detail__rolls">
          <li v-for="(roll, index) in value.rolls" :key="index">{{ rollLine(roll, index) }}</li>
        </ul>
      </template>

      <template v-else-if="value.empty">
        <p class="lore-detail__none">{{ t.empty }}</p>
      </template>
      <template v-else-if="row.type === 'catalog'">
        <p class="lore-detail__value lore-detail__value--picked">{{ value.shown ?? value.text }}</p>
        <p v-if="value.summary" class="lore-detail__summary">{{ value.summary }}</p>
      </template>
      <p v-else class="lore-detail__value" :class="{ 'lore-detail__value--long': row.type === 'longText' }">{{ value.shown ?? value.text }}</p>
    </div>

    <p v-if="showRaceHint" class="lore-detail__game"><LoreIcon name="hint" />{{ fill(t.race_hint, { race: raceHint.name }) }}</p>
    <p v-if="needs" class="lore-detail__game"><LoreIcon name="hint" />{{ fill(t.needs_first, { field: needs.label }) }}</p>

    <!-- What the field takes, as buttons: the keys in the footer do the same. -->
    <div v-if="!row.name && !rolling" class="lore-detail__actions">
      <template v-if="trait === 'none'">
        <RpButton variant="primary" :disabled="readOnly || busy" data-action="roll" @click="emit('roll')">
          <img class="lore-detail__button-die" :src="d20" alt="">{{ t["trait.roll"] }}
        </RpButton>
        <RpButton :disabled="readOnly || busy" data-action="type" @click="emit('edit')">{{ t["trait.type"] }}</RpButton>
      </template>
      <template v-else-if="trait === 'rolled'">
        <RpButton variant="primary" :disabled="readOnly || busy" data-action="keep" @click="emit('keep')">{{ t["trait.keep"] }}</RpButton>
        <RpButton :disabled="readOnly || busy" data-action="roll" @click="emit('roll')">
          <img class="lore-detail__button-die" :src="d20" alt="">{{ t["trait.reroll"] }}
        </RpButton>
      </template>
      <template v-else-if="!trait">
        <div v-if="row.type === 'choice'" class="lore-detail__choices" role="group" :aria-label="row.label">
          <RpChip
            v-for="choice in row.choices"
            :key="choice.id"
            :pressed="value.text === choice.id"
            :disabled="readOnly || busy"
            :data-choice="choice.id"
            @click="emit('choose', choice.id)"
          >
            {{ choice.name }}
          </RpChip>
        </div>
        <RpButton v-else variant="primary" :disabled="readOnly || busy || !!needs" data-action="edit" @click="emit('edit')">{{ editLabel }}</RpButton>
        <RpButton v-if="!value.empty" :disabled="readOnly || busy" data-action="clear" @click="emit('clear')">
          {{ confirmingClear ? t.clear_again : t.clear }}
        </RpButton>
      </template>
    </div>
    <p v-if="trait === 'none' && !rolling" class="lore-detail__warn">{{ t["trait.whichever"] }}</p>
    <p v-else-if="trait === 'rolled' && !rolling" class="lore-detail__warn">{{ t["trait.reroll_note"] }}</p>
    <p v-else-if="trait === 'fixed'" class="lore-detail__fixed">
      <LoreIcon name="owner" />{{ value.rolls?.length ? t["trait.fixed_rolled"] : t["trait.fixed_typed"] }}
    </p>

    <p v-if="row.hint || row.name" class="lore-detail__hint">{{ row.name ? t.name_hint : row.hint }}</p>
  </div>
</template>
