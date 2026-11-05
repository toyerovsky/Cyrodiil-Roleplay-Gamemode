<script setup>
import { computed, nextTick, useTemplateRef, watch } from "vue";
import { RpButton, RpField } from "../rp/index.js";
import { fill } from "../shared/locale.js";

// A field picked from a list: the lore list the server cut for this card, or a field's few choices. The search takes
// the typing, the arrows move through the list (the page reads them), Enter, a double click or Pick takes the one shown.
const props = defineProps({
  field: { type: Object, required: true },
  options: { type: Array, required: true },
  index: { type: Number, required: true },
  loading: Boolean,
  message: { type: String, default: "" },
  chosen: { type: String, default: null },
  gameRace: { type: String, default: null },
  busy: Boolean,
  t: { type: Object, required: true },
});

const emit = defineEmits(["move", "pick", "back"]);
const query = defineModel("query", { type: String, default: "" });

const search = useTemplateRef("search");
const list = useTemplateRef("list");
const optionId = (at) => `lore-option-${at}`;
const shown = computed(() => props.options[props.index] ?? null);
const startsGroup = (at) => !!props.options[at].group && props.options[at].group !== props.options[at - 1]?.group;
// The kind under the name, where it says more than the field's own label (a deity among the faiths, a Khajiit furstock).
const kindOf = (option) => {
  const kind = props.t[`kind.${option.kind}`];
  if (props.field.type !== "catalog" || !kind || kind === props.field.label || option.kind === "province") return "";
  return option.race ? `${kind} · ${option.race}` : kind;
};

watch(
  () => [props.index, props.options],
  async () => {
    await nextTick();
    list.value?.querySelector(`#${optionId(props.index)}`)?.scrollIntoView?.({ block: "nearest" });
  },
);

defineExpose({ focus: () => search.value?.focus() });
</script>

<template>
  <div class="lore-pick">
    <RpField ref="search" v-model="query" class="lore-pick__search" :label="t.search" :placeholder="t.search_placeholder" autocomplete="off" />
    <div
      ref="list"
      class="lore-pick__list"
      role="listbox"
      :aria-label="field.label"
      :aria-activedescendant="options.length ? optionId(index) : undefined"
      :aria-busy="loading"
    >
      <p v-if="loading" class="lore-pick__note" role="status">{{ t.loading_list }}</p>
      <p v-else-if="!options.length" class="lore-pick__note" role="status">{{ message || t.nothing_matches }}</p>
      <template v-for="(option, at) in options" v-else :key="option.id">
        <p v-if="startsGroup(at)" class="lore-list__group" role="presentation">{{ option.group }}</p>
        <div
          :id="optionId(at)"
          role="option"
          class="lore-option"
          :class="{ 'lore-option--cursor': at === index, 'lore-option--chosen': option.id === chosen }"
          :aria-selected="at === index"
          :data-option="option.id"
          @click="emit('move', at)"
          @dblclick="emit('pick', at)"
        >
          <span class="lore-option__name">
            {{ option.name }}
            <small v-if="kindOf(option)">{{ kindOf(option) }}</small>
          </span>
          <span class="lore-option__tags">
            <em v-if="option.origin" class="lore-tag">{{ t["tag.origin"] }}</em>
            <em v-if="option.historical" class="lore-tag lore-tag--old">{{ t["tag.heritage"] }}</em>
            <em v-if="field.id === 'race' && gameRace === option.id" class="lore-tag lore-tag--game">{{ t["tag.in_game"] }}</em>
            <em v-if="option.id === chosen" class="lore-tag lore-tag--chosen">{{ t["tag.chosen"] }}</em>
          </span>
        </div>
      </template>
    </div>

    <div v-if="shown && !loading" class="lore-pick__about">
      <b class="lore-pick__about-name">{{ shown.name }}</b>
      <p v-if="shown.summary" class="lore-pick__summary">{{ shown.summary }}</p>
      <p v-if="shown.origin" class="lore-pick__note">{{ t["note.origin"] }}</p>
      <p v-if="shown.historical" class="lore-pick__note">{{ t["note.heritage"] }}</p>
    </div>
    <div class="lore-detail__actions">
      <RpButton variant="primary" :disabled="!shown || loading || busy" data-action="pick" @click="emit('pick', index)">
        {{ shown ? fill(t.pick_this, { name: shown.name }) : t.pick }}
      </RpButton>
      <RpButton data-action="back" @click="emit('back')">{{ t.back }}</RpButton>
    </div>
  </div>
</template>
