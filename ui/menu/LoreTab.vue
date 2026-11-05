<script setup>
import { computed } from "vue";
import CardView from "../lore/CardView.vue";
import LoreIcon from "../lore/LoreIcon.vue";
import { RpButton, RpChip } from "../rp/index.js";
import { pickTexts } from "../shared/locale.js";

// The lore card at a glance (MenuView.lore): whether the name is public, how strangers see the character, the card as
// everyone sees it and who online knows them. The card's own words (its sections) are the lore page's.
const props = defineProps({
  lore: { type: Object, required: true },
  t: { type: Object, required: true },
  locale: { type: String, required: true },
});

const emit = defineEmits(["open", "name"]);

const words = computed(() => pickTexts(props.locale, "ui.lore."));
const hasPreview = computed(() => (props.lore.preview?.fields?.length ?? 0) > 0);

function pickName(isPublic) {
  if (isPublic !== props.lore.namePublic) emit("name", isPublic);
}
</script>

<template>
  <p class="menu__note">{{ t["lore.lead"] }}</p>
  <RpButton class="menu__lore-open" variant="primary" @click="emit('open')">{{ t["lore.open"] }}</RpButton>

  <section class="menu__group">
    <h2 class="menu__heading">{{ t["lore.name"] }}</h2>
    <div class="menu__lore-name" role="group" :aria-label="t['lore.name']">
      <RpChip class="menu__lore-chip" :pressed="lore.namePublic" data-vis="public" @click="pickName(true)">
        <LoreIcon name="public" />{{ words["vis.public"] }}
      </RpChip>
      <RpChip class="menu__lore-chip" :pressed="!lore.namePublic" data-vis="private" @click="pickName(false)">
        <LoreIcon name="private" />{{ words["vis.private"] }}
      </RpChip>
    </div>
    <p class="menu__lore-help">{{ words[`vis_help.${lore.namePublic ? "name_public" : "name_private"}`] }}</p>
  </section>

  <section class="menu__group">
    <h2 class="menu__heading">{{ t["lore.seen_as"] }}</h2>
    <p class="menu__lore-header">{{ lore.header }}</p>
  </section>

  <section class="menu__group">
    <h2 class="menu__heading">{{ t["lore.known"] }}</h2>
    <ul v-if="lore.known.length" class="menu__lore-known">
      <li v-for="each in lore.known" :key="each.number" class="menu__lore-person">
        <span class="menu__lore-number">[{{ each.number }}]</span>{{ each.name }}
      </li>
    </ul>
    <p v-else class="menu__note">{{ t["lore.known_none"] }}</p>
  </section>

  <section class="menu__group">
    <h2 class="menu__heading">{{ t["lore.preview"] }}</h2>
    <CardView v-if="hasPreview" class="menu__lore-card" :view="lore.preview" :words="words" :show-header="false" />
    <p v-else class="menu__note">{{ t["lore.preview_none"] }}</p>
  </section>
</template>
