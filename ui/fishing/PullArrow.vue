<script setup>
// Which way the fish pulls, as the screen sees it: a run to either side, a dive away from the angler (up the screen), or
// a rest (a ring).
defineProps({ pull: { type: String, required: true } });

const PATHS = {
  Left: "M20 12H5M11 5l-7 7l7 7",
  Right: "M4 12h15M13 5l7 7l-7 7",
  Away: "M12 20V5M5 11l7-7l7 7",
  Rest: "M12 17a5 5 0 1 0 0-10a5 5 0 1 0 0 10z",
};
</script>

<template>
  <svg class="pull-arrow" viewBox="0 0 24 24" aria-hidden="true" :data-pull="pull">
    <path :d="PATHS[pull]" />
  </svg>
</template>
