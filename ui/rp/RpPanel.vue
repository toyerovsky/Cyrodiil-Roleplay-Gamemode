<script setup>
import { useId } from "vue";

// The title slot dresses the title, which still names the panel. A board is a merchant's: the game's wooden board
// with the paper under its brass clip.
defineProps({
  title: { type: String, required: true },
  variant: { type: String, default: "plain", validator: (value) => ["plain", "letter", "board"].includes(value) },
});

const titleId = useId();
</script>

<template>
  <section class="rp-panel" :class="`rp-panel--${variant}`" :aria-labelledby="titleId">
    <h1 :id="titleId" class="rp-panel__title"><slot name="title">{{ title }}</slot></h1>
    <slot />
  </section>
</template>
