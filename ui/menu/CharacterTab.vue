<script setup>
import { formatDate, formatPlayed } from "../shared/format.js";
import { fill } from "../shared/locale.js";

defineProps({
  name: { type: String, required: true },
  character: { type: Object, required: true },
  t: { type: Object, required: true },
  locale: { type: String, required: true },
});
</script>

<template>
  <dl class="menu__facts">
    <div class="menu__fact">
      <dt>{{ t["character.name"] }}</dt>
      <dd>{{ name }}</dd>
    </div>
    <div class="menu__fact">
      <dt>{{ t["character.created"] }}</dt>
      <dd>{{ formatDate(character.createdAt, locale) }}</dd>
    </div>
    <div class="menu__fact">
      <dt>{{ t["character.played"] }}</dt>
      <dd>{{ formatPlayed(character.playedSeconds, t, locale) }}</dd>
    </div>
    <div v-if="character.gold !== null" class="menu__fact">
      <dt>{{ t["character.gold"] }}</dt>
      <dd>{{ fill(t.gold, { count: character.gold.toLocaleString(locale) }) }}</dd>
    </div>
  </dl>
</template>
