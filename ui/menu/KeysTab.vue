<script setup>
import { KEYS } from "./tabs.js";

defineProps({ t: { type: Object, required: true } });
</script>

<template>
  <p class="menu__note">{{ t["keys.lead"] }}</p>
  <dl class="menu__keys">
    <div v-for="each in KEYS" :key="each.id" class="menu__key" :data-key="each.key">
      <dt><kbd class="menu__keycap">{{ each.label ?? each.key }}</kbd></dt>
      <dd>{{ t[`key.${each.id}`] }}</dd>
    </div>
  </dl>
</template>
