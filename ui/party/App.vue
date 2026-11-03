<script setup>
import { computed, onMounted, ref } from "vue";
import { RpIcon } from "../rp/index.js";
import { fill, usePageTexts } from "../shared/locale.js";

const props = defineProps({ bridge: { type: Object, required: true } });

const { t } = usePageTexts(props.bridge, "party");
const members = ref([]);
const max = ref(0);

const rows = computed(() =>
  members.value.map((member) => {
    const down = member.health === 0;
    const away = !member.self && (member.away || member.health === null);
    return { ...member, down, away, mark: describeMark(member, down, away) };
  }),
);

function describeMark(member, down, away) {
  if (down) return t.value.down;
  if (member.self) return t.value.you;
  return away ? t.value.away : "";
}

onMounted(() => {
  props.bridge.onState((view) => {
    members.value = view.members;
    max.value = view.max;
  });
  props.bridge.signalReady();
});
</script>

<template>
  <section v-if="members.length" class="party" :aria-label="t.title">
    <header class="party__head">
      <p class="party__title">{{ t.title }}</p>
      <p class="party__count">{{ fill(t.count, { count: members.length, max }) }}</p>
    </header>
    <ul class="party__members">
      <li
        v-for="(member, index) in rows"
        :key="index"
        class="party__member"
        :class="{ 'party__member--self': member.self, 'party__member--away': member.away, 'party__member--down': member.down }"
      >
        <div class="party__line">
          <span class="party__rank">
            <RpIcon v-if="member.leader" class="party__crown" name="crown" />
            <span v-if="member.leader" class="party__hidden">{{ t.leader }}</span>
          </span>
          <span class="party__name">{{ member.name }}</span>
          <span v-if="member.mark" class="party__mark">{{ member.mark }}</span>
        </div>
        <div
          v-if="member.health !== null"
          class="party__health"
          role="meter"
          :aria-label="fill(t.health, { name: member.name })"
          aria-valuemin="0"
          aria-valuemax="100"
          :aria-valuenow="member.health"
        >
          <div class="party__health-left" :style="{ width: `${member.health}%` }"></div>
        </div>
      </li>
    </ul>
  </section>
</template>
