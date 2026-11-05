<script setup>
import { RpButton } from "../rp/index.js";
import { fill } from "../shared/locale.js";
import { knowsYou, mayIntroduce, relationOf, seesTheirName } from "./card.js";
import LoreIcon from "./LoreIcon.vue";

// One player around and what can be done: read their card, introduce yourself or answer their introduction, or become
// a stranger to them again. An introduction changes nothing until the other player accepts it.
defineProps({
  person: { type: Object, default: null },
  loaded: Boolean,
  range: { type: Number, default: null },
  confirmingForget: Boolean,
  busy: Boolean,
  t: { type: Object, required: true },
});

const emit = defineEmits(["read", "introduce", "accept", "decline", "forget"]);
</script>

<template>
  <div class="lore-detail__field">
    <template v-if="!person">
      <h2 class="lore-detail__title">{{ t["part.nearby"] }}</h2>
      <p class="lore-detail__kind">{{ range ? fill(t["nearby.within"], { range }) : t["nearby.lead"] }}</p>
      <p class="lore-detail__none">{{ loaded ? t["nearby.nobody_long"] : t["nearby.looking"] }}</p>
    </template>
    <template v-else>
      <h2 class="lore-detail__title">{{ person.header }}</h2>
      <p class="lore-detail__kind">{{ fill(t["nearby.away"], { meters: person.meters }) }}</p>
      <p class="lore-vis lore-vis--pill" :class="seesTheirName(person) ? 'lore-vis--private' : 'lore-vis--public'">
        <LoreIcon :name="seesTheirName(person) ? 'private' : 'public'" />{{ t[`relation.${relationOf(person)}`] }}
      </p>
      <p class="lore-detail__help">{{ t[`what.${relationOf(person)}`] }}</p>
      <p v-if="person.outgoing" class="lore-detail__game"><LoreIcon name="hint" />{{ t["nearby.outgoing_note"] }}</p>

      <div class="lore-detail__actions lore-detail__actions--column">
        <RpButton variant="primary" :disabled="busy" data-action="read" @click="emit('read')">
          <kbd>Enter</kbd>{{ person.hasCard === false ? t["nearby.read_none"] : t["nearby.read"] }}
        </RpButton>
        <template v-if="person.incoming">
          <RpButton variant="primary" :disabled="busy" data-action="accept" @click="emit('accept')"><kbd>=</kbd>{{ t["nearby.accept"] }}</RpButton>
          <RpButton :disabled="busy" data-action="decline" @click="emit('decline')"><kbd>-</kbd>{{ t["nearby.decline"] }}</RpButton>
        </template>
        <RpButton v-else-if="mayIntroduce(person)" :disabled="busy" data-action="introduce" @click="emit('introduce')">
          <kbd>=</kbd>{{ t["nearby.introduce"] }}
        </RpButton>
        <RpButton v-if="knowsYou(person)" :disabled="busy" data-action="forget" @click="emit('forget')">
          <kbd>Del</kbd>{{ confirmingForget ? t["nearby.forget_again"] : t["nearby.forget"] }}
        </RpButton>
      </div>
      <p v-if="mayIntroduce(person)" class="lore-detail__help">{{ t["nearby.introduce_note"] }}</p>
      <p class="lore-detail__hint">{{ t["nearby.in_game"] }}</p>
    </template>
  </div>
</template>
