<script setup>
defineProps({
  help: { type: Array, required: true },
  t: { type: Object, required: true },
});
</script>

<template>
  <p class="menu__note">{{ t["help.lead"] }}</p>
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
