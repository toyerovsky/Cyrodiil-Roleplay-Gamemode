<script setup>
import { nextTick, useTemplateRef, watch } from "vue";
import { fill } from "../shared/locale.js";
import { relationOf, seesTheirName } from "./card.js";
import LoreIcon from "./LoreIcon.vue";

// The players around, as each lets this player see them: a stranger by their race, someone known by their name.
const props = defineProps({
  players: { type: Array, required: true },
  loaded: Boolean,
  cursor: { type: Number, required: true },
  label: { type: String, required: true },
  t: { type: Object, required: true },
});

const emit = defineEmits(["select", "open"]);

const list = useTemplateRef("list");
const optionId = (index) => `lore-person-${index}`;

function tagOf(person) {
  if (person.incoming) return { text: props.t["nearby.incoming"], kind: "incoming" };
  if (person.outgoing) return { text: props.t["nearby.outgoing"], kind: "outgoing" };
  const relation = relationOf(person);
  return relation === "stranger" ? null : { text: props.t[`relation.${relation}`], kind: relation === "known" ? "known" : "half" };
}

watch(
  () => [props.cursor, props.players],
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
    :aria-activedescendant="players.length ? optionId(cursor) : undefined"
  >
    <p v-if="!loaded" class="lore-list__note" role="status">{{ t["nearby.looking"] }}</p>
    <p v-else-if="!players.length" class="lore-list__note">{{ t["nearby.nobody"] }}</p>
    <div
      v-for="(person, index) in players"
      v-else
      :id="optionId(index)"
      :key="person.id"
      role="option"
      class="lore-row lore-row--person"
      :class="{ 'lore-row--cursor': index === cursor }"
      :aria-selected="index === cursor"
      :data-player="person.id"
      @click="emit('select', index)"
      @dblclick="emit('open', index)"
    >
      <span class="lore-row__label">
        <LoreIcon class="lore-row__mark" :name="seesTheirName(person) ? 'private' : 'public'" />
        {{ person.header }}
      </span>
      <span class="lore-row__value">
        <em v-if="tagOf(person)" class="lore-tag" :class="`lore-tag--${tagOf(person).kind}`">{{ tagOf(person).text }}</em>
      </span>
      <span class="lore-row__meters">{{ fill(t["nearby.meters"], { meters: person.meters }) }}</span>
    </div>
  </div>
</template>
