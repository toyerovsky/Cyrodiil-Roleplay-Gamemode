<script setup>
import { PICTURES } from "../guides/pictures.js";

defineProps({
  help: { type: Array, required: true },
  guides: { type: Array, required: true },
  t: { type: Object, required: true },
});

defineEmits(["read"]);
</script>

<template>
  <section v-if="guides.length" class="menu__group" data-guides>
    <h2 class="menu__heading">{{ t["guides.heading"] }}</h2>
    <p class="menu__note">{{ t["guides.lead"] }}</p>
    <ul class="menu__guides">
      <li v-for="guide in guides" :key="guide.topic">
        <button type="button" class="menu__guide" :data-topic="guide.topic" @click="$emit('read', guide.topic)">
          <img class="menu__guide-picture" :src="PICTURES[guide.topic]" alt="">
          <span class="menu__guide-title">{{ guide.title }}</span>
          <span :class="['menu__guide-state', { 'menu__guide-state--new': !guide.seen }]">
            {{ guide.seen ? t["guides.seen"] : t["guides.new"] }}
          </span>
        </button>
      </li>
    </ul>
  </section>
  <p class="menu__note menu__note--commands">{{ t["help.lead"] }}</p>
  <section v-for="group in help" :key="group.category" class="menu__group" :data-category="group.category">
    <h2 class="menu__heading">{{ t[`category.${group.category}`] }}</h2>
    <dl class="menu__commands">
      <div v-for="command in group.commands" :key="command.syntax" class="menu__command">
        <dt class="menu__syntax">{{ command.syntax }}</dt>
        <dd class="menu__description">{{ command.description }}</dd>
      </div>
    </dl>
  </section>
</template>
