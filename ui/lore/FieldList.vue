<script setup>
import { nextTick, useTemplateRef, watch } from "vue";
import { fill } from "../shared/locale.js";
import { traitState, traitWord, visibilityOf } from "./card.js";
import LoreIcon from "./LoreIcon.vue";

// The fields of one section, the cursor on one of them. The list holds the focus while the player browses: the arrows
// move the cursor (the page reads them), a click puts it on a row, a double click edits the row.
const props = defineProps({
  rows: { type: Array, required: true },
  cursor: { type: Number, required: true },
  values: { type: Object, required: true },
  name: { type: String, default: "" },
  namePublic: { type: Boolean, default: false },
  label: { type: String, required: true },
  t: { type: Object, required: true },
});

const emit = defineEmits(["select", "open"]);

const list = useTemplateRef("list");
const optionId = (index) => `lore-row-${index}`;

const valueOf = (field) => props.values[field.id] ?? { empty: true, vis: field.vis };
const visOf = (row) => (row.name ? (props.namePublic ? "public" : "private") : visibilityOf(row, valueOf(row)));
const startsGroup = (index) => !!props.rows[index].group && props.rows[index].group !== props.rows[index - 1]?.group;
const shownOf = (row) => (valueOf(row).shown ?? valueOf(row).text)?.replace(/\s+/g, " ") ?? "";

watch(
  () => [props.cursor, props.rows],
  async () => {
    await nextTick();
    list.value?.querySelector(`#${optionId(props.cursor)}`)?.scrollIntoView?.({ block: "nearest" });
  },
);

defineExpose({ focus: () => list.value?.focus() });
</script>

<template>
  <div
    ref="list"
    class="lore-list"
    role="listbox"
    tabindex="0"
    :aria-label="label"
    :aria-activedescendant="rows.length ? optionId(cursor) : undefined"
  >
    <template v-for="(row, index) in rows" :key="row.id">
      <p v-if="startsGroup(index)" class="lore-list__group" role="presentation">{{ row.group }}</p>
      <div
        :id="optionId(index)"
        role="option"
        class="lore-row"
        :class="{ 'lore-row--cursor': index === cursor, 'lore-row--unset': !row.name && valueOf(row).empty, 'lore-row--trait': row.type === 'trait' }"
        :aria-selected="index === cursor"
        :data-field="row.id"
        @click="emit('select', index)"
        @dblclick="emit('open', index)"
      >
        <span class="lore-row__label">
          <LoreIcon v-if="row.type === 'trait'" class="lore-row__mark" name="die" />
          <LoreIcon v-else-if="row.type === 'catalog'" class="lore-row__mark" name="list" />
          {{ row.name ? t.name : row.label }}
        </span>
        <span v-if="row.name" class="lore-row__value lore-row__value--name">{{ name || t.no_name }}</span>
        <span v-else-if="row.type === 'trait' && valueOf(row).number != null" class="lore-row__value lore-trait">
          <b class="lore-trait__number">{{ valueOf(row).number }}</b>
          <span class="lore-trait__bar" aria-hidden="true"><span :style="{ width: `${valueOf(row).number}%` }" /></span>
          <span class="lore-trait__word">{{ t[`word.${traitWord(valueOf(row).number)}`] }}</span>
          <em v-if="traitState(valueOf(row)) === 'rolled'" class="lore-tag">{{ t["trait.one_reroll"] }}</em>
        </span>
        <span v-else-if="valueOf(row).empty" class="lore-row__value lore-row__value--unset">{{ t.not_set }}</span>
        <span v-else class="lore-row__value">{{ shownOf(row) }}</span>
        <span class="lore-vis" :class="`lore-vis--${visOf(row)}`" :title="fill(t.who_sees_it, { vis: t[`vis.${visOf(row)}`] })">
          <LoreIcon :name="visOf(row)" />
          <span>{{ t[`vis.${visOf(row)}`] }}</span>
        </span>
      </div>
    </template>
  </div>
</template>
