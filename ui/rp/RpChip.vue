<script setup>
// A pressed chip stays picked (aria-pressed), one without `pressed` acts at once. Vue would cast a missing Boolean to
// false and mark an acting chip as a toggle, hence the undefined default.
defineProps({ pressed: { type: Boolean, default: undefined } });
</script>

<template>
  <button type="button" class="rp-chip" :class="{ 'rp-chip--on': pressed }" :aria-pressed="pressed">
    <slot />
  </button>
</template>
