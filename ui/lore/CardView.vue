<script setup>
import { computed } from "vue";
import { groupView } from "./card.js";

// A card as a viewer sees it (the server's PageJson.View): its header line and the fields shown, under their sections.
// The labels come translated, the sections' names are the card's own words. A slot puts a button by each field (the
// staff's Clear).
const props = defineProps({
  view: { type: Object, required: true },
  words: { type: Object, required: true },
  headingLevel: { type: Number, default: 3 },
  showHeader: { type: Boolean, default: true },
});

const groups = computed(() => groupView(props.view.fields ?? []));
const heading = computed(() => `h${props.headingLevel}`);
</script>

<template>
  <div class="lore-view">
    <p v-if="showHeader && view.header" class="lore-view__header">{{ view.header }}</p>
    <p v-if="!groups.length" class="lore-view__none">{{ words.nothing_more }}</p>
    <section v-for="group in groups" :key="group.part" class="lore-view__part">
      <component :is="heading" class="lore-view__heading">{{ words[`part.${group.part}`] }}</component>
      <dl class="lore-view__fields">
        <div v-for="field in group.fields" :key="field.id" class="lore-view__field" :class="{ 'lore-view__field--long': field.long }" :data-field="field.id">
          <dt>{{ field.label }}</dt>
          <dd>{{ field.text }}</dd>
          <slot name="field" :field="field" />
        </div>
      </dl>
    </section>
  </div>
</template>
