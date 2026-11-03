<script setup>
defineProps({
  variant: { type: String, default: "default", validator: (value) => ["default", "primary"].includes(value) },
  type: { type: String, default: "button" },
});
</script>

<template>
  <button :type="type" class="rp-button" :class="`rp-button--${variant}`">
    <slot />
  </button>
</template>
