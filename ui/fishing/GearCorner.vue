<script setup>
import { computed } from "vue";
import { RpIcon } from "../rp/index.js";
import { BAIT_PICTURES, findPicture, ROD_PICTURES } from "../shared/fishing/pictures.js";
import { fill } from "../shared/locale.js";

// The angler's gear and skill, in a corner for as long as they fish: the rod and how worn it is, the bait on the hook
// and how many are left, and the skill's level with the way to the next.
const props = defineProps({
  view: { type: Object, required: true },
  t: { type: Object, required: true },
  locale: { type: String, required: true },
});

const PERCENT = 100;

const rod = computed(() => props.view.rod);
// The bait chosen is on the hook while some is left; once it runs out the hook goes bare, and the corner names what ran
// out.
const bait = computed(() => (props.view.bait && props.view.bait.count > 0 ? props.view.bait : null));
const ranOut = computed(() => (props.view.bait && props.view.bait.count === 0 ? props.view.bait : null));
const skill = computed(() => props.view.skill);
const reach = computed(() =>
  fill(props.t.reach, { metres: (rod.value.reachCm / PERCENT).toLocaleString(props.locale, { maximumFractionDigits: 1 }) }),
);
// The last level has no next: its bar stays full.
const progress = computed(() => (skill.value.xpNext > 0 ? Math.min(1, skill.value.xp / skill.value.xpNext) : 1));
</script>

<template>
  <aside class="fishing-gear rp-plate" :aria-label="t.gear">
    <div class="fishing-gear__row" data-gear="rod">
      <img class="fishing-gear__picture" :src="findPicture(ROD_PICTURES[rod.tier])" alt="" />
      <p class="fishing-gear__text">
        <span class="fishing-gear__name">{{ rod.name }}</span>
        <span class="fishing-gear__fact">
          <span class="fishing-gear__condition" :class="`fishing-gear__condition--${rod.condition.toLowerCase()}`">{{ t[`condition.${rod.condition}`] }}</span>
          <span aria-hidden="true">·</span>
          <span>{{ reach }}</span>
        </span>
      </p>
    </div>

    <div class="fishing-gear__row" :class="{ 'fishing-gear__row--none': !bait }" data-gear="bait">
      <img v-if="bait" class="fishing-gear__picture" :src="findPicture(BAIT_PICTURES[bait.kind])" alt="" />
      <span v-else class="fishing-gear__picture fishing-gear__picture--none"><RpIcon name="bait" /></span>
      <p class="fishing-gear__text">
        <span class="fishing-gear__name">{{ bait ? bait.name : t.bait_none }}</span>
        <span v-if="bait" class="fishing-gear__fact">{{ fill(t.bait_left, { count: bait.count.toLocaleString(locale) }) }}</span>
        <span v-else-if="ranOut" class="fishing-gear__fact" data-bait-out>{{ fill(t.bait_out, { bait: ranOut.name }) }}</span>
        <span v-else class="fishing-gear__fact">{{ t.bait_none_hint }}</span>
      </p>
    </div>

    <div class="fishing-gear__skill" data-gear="skill">
      <p class="fishing-gear__skill-head">
        <span>{{ t.skill }}</span>
        <strong>{{ fill(t.level, { level: skill.level }) }}</strong>
      </p>
      <div class="fishing-gear__xp" role="meter" :aria-label="t.skill" aria-valuemin="0" :aria-valuemax="Math.max(1, skill.xpNext)" :aria-valuenow="skill.xpNext > 0 ? skill.xp : 1">
        <span :style="{ width: `${Math.round(progress * PERCENT * 10) / 10}%` }"></span>
      </div>
      <p class="fishing-gear__xp-text">
        {{ skill.xpNext > 0 ? fill(t.xp_of, { xp: skill.xp.toLocaleString(locale), next: skill.xpNext.toLocaleString(locale) }) : t.xp_top }}
      </p>
    </div>
  </aside>
</template>
