<script setup>
import { ICONS } from "./icons.js";

defineProps({ name: { type: String, required: true } });
</script>

<template>
  <svg class="rp-icon" viewBox="0 0 24 24" aria-hidden="true">
    <path :d="ICONS[name] ?? ICONS.question" />
  </svg>
</template>
