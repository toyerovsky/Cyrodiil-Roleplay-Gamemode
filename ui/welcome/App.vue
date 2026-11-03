<script setup>
import { computed, nextTick, onMounted, onUnmounted, ref, useTemplateRef } from "vue";
import crest from "../rp/art/crest.webp";
import seal from "../rp/art/seal.webp";
import { RpButton, RpIlluminated, RpPanel } from "../rp/index.js";
import { formatDate, formatPlayed } from "../shared/format.js";
import { fill, usePageTexts } from "../shared/locale.js";
import { isComposing, isHeldPress, readCode } from "../shared/press.js";

const props = defineProps({ bridge: { type: Object, required: true } });

const PLAY = "KeyE";
const NEW = "KeyN";
const ENTER = ["Enter", "NumpadEnter"];

const { locale, t } = usePageTexts(props.bridge, "welcome");

const welcome = ref(null);
// What the player chose, "play" or "new", until the game takes over.
const chosen = ref(null);
const slots = useTemplateRef("slots");

const character = computed(() => welcome.value?.characters[0] ?? null);
const canCreate = computed(() => (welcome.value?.emptySlots ?? 0) > 0);

const title = computed(() => {
  const { firstVisit, nickname } = welcome.value;
  if (firstVisit) return t.value.title_first;
  return nickname ? fill(t.value.title_back, { name: nickname }) : t.value.title_back_unnamed;
});
const lead = computed(() => (character.value ? t.value.lead_back : t.value.lead_first));

const joinName = (each) => `${each.firstName} ${each.lastName}`;

function play() {
  if (!character.value || chosen.value) return;
  chosen.value = "play";
  props.bridge.play();
}

function create() {
  if (!canCreate.value || chosen.value) return;
  chosen.value = "new";
  props.bridge.createCharacter();
}

// Read by the key's place: in the game a key comes without its code (readCode). Enter does what the welcome offers.
function onKeydown(event) {
  if (!welcome.value || isComposing(event)) return;
  const code = readCode(event);
  const enter = ENTER.includes(code);
  if (code !== PLAY && code !== NEW && !enter) return;

  event.preventDefault();
  if (isHeldPress(event)) return;
  if (code === NEW) create();
  else if (code === PLAY || character.value) play();
  else create();
}

async function onWelcome(view) {
  welcome.value = view;
  chosen.value = null;
  await nextTick();
  slots.value?.querySelector("button")?.focus();
}

onMounted(() => {
  props.bridge.onWelcome(onWelcome);
  props.bridge.onClosed(() => {
    welcome.value = null;
    chosen.value = null;
  });
  window.addEventListener("keydown", onKeydown);
  props.bridge.signalReady();
});

onUnmounted(() => window.removeEventListener("keydown", onKeydown));
</script>

<template>
  <div v-if="welcome" class="welcome">
    <div class="welcome__document">
      <img class="welcome__crest" :src="crest" alt="">
      <RpPanel class="welcome__panel" variant="letter" :title="title">
        <p class="welcome__lead">{{ lead }}</p>

        <ul ref="slots" class="welcome__slots" :aria-label="t.slots">
          <li v-for="each in welcome.characters" :key="joinName(each)" class="welcome__slot welcome__slot--character">
            <h2 class="welcome__name"><RpIlluminated tag="span" :text="joinName(each)" /></h2>
            <dl class="welcome__facts">
              <div class="welcome__fact">
                <dt>{{ t.last_played }}</dt>
                <dd>{{ formatDate(each.lastPlayedAt, locale) }}</dd>
              </div>
              <div class="welcome__fact">
                <dt>{{ t.time_played }}</dt>
                <dd>{{ formatPlayed(each.playedSeconds, t, locale) }}</dd>
              </div>
            </dl>
            <RpButton class="welcome__action" variant="primary" :disabled="!!chosen" :aria-busy="chosen === 'play'" @click="play">
              {{ chosen === "play" ? t.entering : t.play }}
            </RpButton>
          </li>

          <li v-for="slot in welcome.emptySlots" :key="`empty-${slot}`" class="welcome__slot welcome__slot--empty">
            <h2 class="welcome__slot-title">{{ t.empty }}</h2>
            <p class="welcome__note">{{ t.empty_note }}</p>
            <RpButton class="welcome__action" variant="primary" :disabled="!!chosen" :aria-busy="chosen === 'new'" @click="create">
              {{ chosen === "new" ? t.starting : t.new }}
            </RpButton>
          </li>

          <li v-for="slot in welcome.lockedSlots" :key="`locked-${slot}`" class="welcome__slot welcome__slot--locked">
            <img class="welcome__seal" :src="seal" alt="">
            <h2 class="welcome__slot-title">{{ t.locked }}</h2>
          </li>
        </ul>

        <p class="welcome__keys">{{ character ? t.keys_play : t.keys_new }}</p>
      </RpPanel>
    </div>
  </div>
</template>
