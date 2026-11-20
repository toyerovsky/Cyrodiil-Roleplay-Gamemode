<script setup>
import { computed, ref } from "vue";
import { RpButton, RpField } from "../rp/index.js";
import { fill, pickTexts } from "../shared/locale.js";
import History from "../gambling/History.vue";
const props = defineProps({ page: { type: Object, default: null }, locale: { type: String, required: true }, canCancel: Boolean });
const emit = defineEmits(["search", "cancel"]);
const t = computed(() => pickTexts(props.locale, "ui.gambling."));
const name = ref("");
const before = ref(null);
function search() { before.value = null; emit("search", { player: name.value.trim() || null }); }
function older() { before.value = props.page.games.at(-1).id; emit("search", null, before.value); }
</script>

<template>
  <div class="admin-audit admin-gambling">
    <form class="admin-dice__filter" @submit.prevent="search"><RpField v-model="name" :label="t.filter" /><RpButton type="submit" variant="primary">{{ t.search }}</RpButton></form>
    <p v-if="!page" role="status">{{ t.waiting }}</p>
    <History v-else :games="page.games" :locale="locale" staff>
      <template #default="{ game }"><RpButton v-if="game.status === 'Open'" :disabled="!canCancel" @click="emit('cancel', game.id)">{{ fill(t.cancel, { id: game.id }) }}</RpButton></template>
    </History>
    <div class="admin-gambling__pages"><RpButton v-if="page?.more" @click="older">{{ t.older }}</RpButton><RpButton v-if="before" @click="search">{{ t.newest }}</RpButton></div>
  </div>
</template>

<style>
.admin-gambling { grid-template-rows: auto minmax(0, 1fr) auto; }
.admin-gambling > .menu__group { min-height: 0; overflow-y: auto; margin: 0; }
.admin-gambling .admin-dice__filter { grid-template-columns: minmax(0, 1fr) auto; }
.admin-gambling__pages { display: flex; justify-content: center; gap: 12px; }
</style>
