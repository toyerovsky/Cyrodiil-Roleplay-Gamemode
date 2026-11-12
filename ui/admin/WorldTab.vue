<script setup>
import { computed, reactive, ref } from "vue";
import { RpButton, RpChip, RpField } from "../rp/index.js";
import { fill } from "../shared/locale.js";
import { formatMoment } from "./format.js";

const props = defineProps({
  view: { type: Object, required: true },
  // Null until the server answered.
  notices: { type: Array, default: null },
  t: { type: Object, required: true },
  locale: { type: String, required: true },
  may: { type: Function, required: true },
  can: { type: Function, required: true },
});

const emit = defineEmits(["act"]);

const KINDS = ["gate", "city", "settlement", "inn"];
const AXES = ["x", "y", "z"];
// What the side column holds: the sky, the spawn and a spot to go to.
const SIDE_ACTIONS = ["time", "weather", "spawnHere", "spot"];
const WHOLE = /^-?\d+$/;

const hour = ref("");
const query = ref("");
const spot = reactive({ x: "", y: "", z: "" });
const spotProblem = ref(false);

const groups = computed(() => {
  const text = query.value.trim().toLowerCase();
  return KINDS.map((kind) => ({
    kind,
    places: props.view.places.filter((place) => place.kind === kind && (text === "" || place.name.toLowerCase().includes(text))),
  })).filter((group) => group.places.length);
});

const act = (action, values, name) => emit("act", action, values, name);

// Within the staff member's own area: the server moves nobody to another.
const isHere = (trader) => trader.area === props.view.area;

function goToSpot() {
  spotProblem.value = !AXES.every((axis) => WHOLE.test(spot[axis].trim()));
  if (!spotProblem.value) act("spot", Object.fromEntries(AXES.map((axis) => [axis, spot[axis].trim()])), props.t["world.spot"]);
}
</script>

<template>
  <div class="admin-world">
    <div v-if="SIDE_ACTIONS.some(may)" class="admin-world__side">
      <section v-if="may('time') || may('weather')" class="admin-world__sky">
        <form v-if="may('time')" class="admin-world__time" @submit.prevent="act('time', { hour: hour.trim() }, hour)">
          <h3 class="admin__heading">{{ t["world.time"] }}</h3>
          <div class="admin-world__row">
            <RpField v-model="hour" :label="t['world.hour']" inputmode="numeric" />
            <RpButton type="submit" :disabled="!can('time') || !hour.trim()">{{ t["action.set"] }}</RpButton>
          </div>
        </form>
        <div v-if="may('weather')">
          <h3 class="admin__heading">{{ t["world.weather"] }}</h3>
          <div class="admin-chips" role="group" :aria-label="t['world.weather']">
            <RpChip
              v-for="weather in view.weathers"
              :key="weather"
              :disabled="!can('weather')"
              @click="act('weather', { weather }, t[`weather.${weather}`])"
            >
              {{ t[`weather.${weather}`] ?? weather }}
            </RpChip>
          </div>
        </div>
      </section>

      <section v-if="may('spawnHere')" class="admin-world__spawn">
        <h3 class="admin__heading">{{ t["world.spawn"] }}</h3>
        <p class="admin__note">{{ t["world.spawn_note"] }}</p>
        <p v-if="view.spawn" class="admin-world__spawn-at">
          {{ fill(t["world.spawn_at"], { position: `${view.spawn.x} ${view.spawn.y} ${view.spawn.z}` }) }}
        </p>
        <p v-else class="admin__note">{{ t["world.no_spawn"] }}</p>
        <div class="admin-world__buttons">
          <RpButton :disabled="!can('spawnHere')" @click="act('spawnHere', {}, t['world.spawn'])">{{ t["action.spawn_here"] }}</RpButton>
          <RpButton v-if="view.spawn" :disabled="!can('spawnGo')" @click="act('spawnGo', {}, t['world.spawn'])">{{ t["action.go"] }}</RpButton>
        </div>
      </section>

      <section v-if="may('spot')" class="admin-world__spot">
        <form @submit.prevent="goToSpot">
          <h3 class="admin__heading">{{ t["world.spot"] }}</h3>
          <p class="admin__note">{{ t["world.spot_note"] }}</p>
          <div class="admin-world__row admin-world__row--spot">
            <RpField v-for="axis in AXES" :key="axis" v-model="spot[axis]" :label="axis" inputmode="numeric" :invalid="spotProblem" />
            <RpButton type="submit" :disabled="!can('spot')">{{ t["action.go"] }}</RpButton>
          </div>
          <p v-if="spotProblem" class="admin__problem" role="alert">{{ t["world.spot_whole"] }}</p>
        </form>
      </section>
    </div>

    <section v-if="may('place')" class="admin-world__places">
      <h3 class="admin__heading">{{ t["world.places"] }}</h3>
      <p class="admin__note">{{ t["world.outside"] }}</p>
      <RpField v-model="query" :label="t['world.places_filter']" type="search" />
      <div v-for="group in groups" :key="group.kind" class="admin-world__group">
        <h4 class="admin-world__kind">{{ t[`place.${group.kind}`] }}</h4>
        <ul class="admin-world__list">
          <li v-for="place in group.places" :key="place.id">
            <button type="button" class="admin-place" :disabled="!can('place')" :data-place="place.id" @click="act('place', { place: place.id }, place.name)">
              {{ place.name }}
            </button>
          </li>
        </ul>
      </div>
      <p v-if="!groups.length" class="admin__note">{{ t["world.no_places"] }}</p>
    </section>

    <section v-if="may('spot')" class="admin-world__traders">
      <h3 class="admin__heading">{{ t["world.traders"] }}</h3>
      <ul v-if="view.traders.length" class="admin-items">
        <li v-for="trader in view.traders" :key="trader.id" class="admin-item">
          <span class="admin-item__name">{{ trader.shop }}</span>
          <span class="admin-item__code">{{ trader.area }}</span>
          <span class="admin-item__code">{{ trader.x }} {{ trader.y }} {{ trader.z }}</span>
          <RpButton
            v-if="isHere(trader)"
            class="admin-item__button"
            :disabled="!can('spot')"
            @click="act('spot', { x: trader.x, y: trader.y, z: trader.z }, trader.shop)"
          >
            {{ t["action.go"] }}
          </RpButton>
          <span v-else class="admin-item__worn">{{ t["world.elsewhere"] }}</span>
        </li>
      </ul>
      <p v-else class="admin__note">{{ t["world.no_traders"] }}</p>
    </section>

    <section v-if="may('noticeRemove')" class="admin-world__notices">
      <h3 class="admin__heading">{{ t["world.notices"] }}</h3>
      <p v-if="!notices" class="admin__note" role="status">{{ t.loading }}</p>
      <p v-else-if="!notices.length" class="admin__note">{{ t["world.no_notices"] }}</p>
      <ul v-else class="admin-items">
        <li v-for="notice in notices" :key="notice.id" class="admin-item admin-notice" :data-notice="notice.id">
          <div class="admin-notice__body">
            <p class="admin-notice__text">{{ notice.text }}</p>
            <p class="admin-notice__meta">
              {{ fill(t["world.notice_meta"], { author: notice.author, board: notice.board, area: notice.area, date: formatMoment(notice.posted, locale) }) }}
            </p>
          </div>
          <RpButton class="admin-item__button" :disabled="!can('noticeRemove')" @click="act('noticeRemove', { id: notice.id }, notice.author)">
            {{ t["action.notice_remove"] }}
          </RpButton>
        </li>
      </ul>
    </section>
  </div>
</template>
