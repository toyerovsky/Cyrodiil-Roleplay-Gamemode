<script setup>
import { computed } from "vue";

// A span inside a heading, a paragraph elsewhere.
const props = defineProps({
  text: { type: String, required: true },
  tag: { type: String, default: "p", validator: (value) => ["p", "span"].includes(value) },
});

// The illuminated letters of the game's books, a to z. A text that starts with anything else starts plainly.
const INITIALS = import.meta.glob("./art/initials/*.webp", { eager: true, query: "?url", import: "default" });

const initial = computed(() => INITIALS[`./art/initials/${props.text.charAt(0).toLowerCase()}.webp`]);
</script>

<template>
  <component :is="tag" class="rp-illuminated" :class="{ 'rp-illuminated--initial': initial }">
    <img v-if="initial" class="rp-illuminated__initial" :src="initial" alt="">{{ text }}
  </component>
</template>
