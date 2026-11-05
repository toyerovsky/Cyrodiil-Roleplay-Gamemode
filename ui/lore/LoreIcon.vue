<script setup>
import { LORE_ICONS } from "./icons.js";

defineProps({ name: { type: String, required: true } });
</script>

<template>
  <svg class="rp-icon lore-icon" viewBox="0 0 24 24" aria-hidden="true">
    <path :d="LORE_ICONS[name] ?? LORE_ICONS.hint" />
  </svg>
</template>
