<script setup>
import { computed, watch } from "vue";
import { RpIcon } from "../rp/index.js";
import { formatLeft, useCountdown } from "../sanctuary/clock.js";
import { fill } from "../shared/locale.js";

// The player's place in the Sanctuary (MenuView.sanctuary) and its rules in short, with the numbers the server runs it by.
const props = defineProps({
  sanctuary: { type: Object, required: true },
  t: { type: Object, required: true },
});

const RULES = ["npcs", "players", "aggressor", "bounty", "victim", "arena"];

const { left, start } = useCountdown();
watch(() => props.sanctuary, (view) => start(view.aggressor ? view.secondsLeft : 0), { immediate: true });

const look = computed(() => {
  if (!props.sanctuary.enabled) return "off";
  if (props.sanctuary.aggressor) return "aggressor";
  return props.sanctuary.inside ? "protected" : "outside";
});
const rules = computed(() => RULES.filter((rule) => rule !== "bounty" || props.sanctuary.bounty > 0));
</script>

<template>
  <p class="menu__note">{{ t["sanctuary.lead"] }}</p>

  <section class="menu__group">
    <h2 class="menu__heading">{{ t["sanctuary.state"] }}</h2>
    <div class="menu__sanctuary" :class="`menu__sanctuary--${look}`" role="status">
      <RpIcon class="menu__sanctuary-shield" name="shield" />
      <div>
        <p class="menu__sanctuary-state">{{ fill(t[`sanctuary.${look}`], { area: sanctuary.area ?? "" }) }}</p>
        <p v-if="look === 'aggressor'" class="menu__sanctuary-left">{{ fill(t["sanctuary.left"], { time: formatLeft(left) }) }}</p>
      </div>
    </div>
  </section>

  <section class="menu__group">
    <h2 class="menu__heading">{{ t["sanctuary.rules"] }}</h2>
    <ul class="menu__sanctuary-rules">
      <li v-for="rule in rules" :key="rule">
        {{ fill(t[`sanctuary.rule.${rule}`], { minutes: sanctuary.aggressorMinutes, bounty: sanctuary.bounty }) }}
      </li>
    </ul>
  </section>
</template>
