<script setup>
import { computed, nextTick, onMounted, onUnmounted, ref, useTemplateRef } from "vue";
import d20 from "../rp/art/d20.webp";
import { RpButton, RpChip, RpPanel } from "../rp/index.js";
import { fill, usePageTexts } from "../shared/locale.js";
import { isComposing, isHeldPress, readCode } from "../shared/press.js";
import { createBoard } from "./board/scene.js";
import { DEFAULT_SET, isSet, SETS } from "./board/sets.js";
import { Sound } from "./board/sound.js";
import * as Tray from "./board/tray.js";
import { iconOf } from "./icons.js";
import { formatAsked, formatDice, sumDice, toDice } from "./notation.js";

const props = defineProps({ bridge: { type: Object, required: true } });

const { locale, t } = usePageTexts(props.bridge, "dice");

const KINDS = Tray.KINDS;
// A roll shown for something else stays this long after it landed.
const PRESENT_MS = 6000;
const FLASH_MS = 1400;
const RECENT_SHOWN = 8;
const NATURAL_HIGH = 20;
const NATURAL_LOW = 1;
const TRAY_KEY = "cyrodiilrp.dice.tray";
const DARK_KEY = "cyrodiilrp.dice.dark";
// The game hands a page the Windows key code alone: the keys readCode does not know.
const KEY_CODES = { 32: "Space", 49: "Digit1", 50: "Digit2", 51: "Digit3", 52: "Digit4", 53: "Digit5", 77: "KeyM", 86: "KeyV" };
const ADDS = { Digit1: "d6", Digit2: "d10", Digit3: "d20", Digit4: "d100" };

const storage = {
  read(key) {
    try {
      return localStorage.getItem(key);
    } catch {
      return null;
    }
  },
  write(key, value) {
    try {
      localStorage.setItem(key, value);
    } catch {
      // Without storage the choice lasts until the page reloads.
    }
  },
};

const sound = new Sound();
const canvas = useTemplateRef("canvas");
const tags = useTemplateRef("tags");
const stage = useTemplateRef("stage");
const throwButton = useTemplateRef("throwButton");

const view = ref(null);
const open = ref(false);
const presenting = ref(null);
const failed = ref(false);
const tray = ref(Tray.parse(storage.read(TRAY_KEY)) ?? Tray.fresh());
const cursor = ref(KINDS.indexOf("d20"));
const set = ref(DEFAULT_SET);
const dark = ref(storage.read(DARK_KEY) === "1");
const muted = ref(sound.muted);
const busy = ref(false);
const banner = ref(null);
const flash = ref("");

let board = null;
let nextRequest = 1;
let pendingId = 0;
let pendingView = null;
let presentReason = "";
let presentTimer = 0;
let flashTimer = 0;

const maxDice = computed(() => view.value?.maxDice ?? Tray.MAX_DICE);
const counts = computed(() => Tray.counts(tray.value));
const total = computed(() => Tray.total(tray.value));
const asked = computed(() => formatAsked(toDice(counts.value, []).map((die) => ({ ...die, value: 0 })), t.value));
const countText = computed(() => flash.value || (tray.value.placeholder ? t.value.placeholder : fill(t.value.count, { count: total.value, max: maxDice.value })));
const shown = computed(() => open.value || presenting.value !== null);

const sets = computed(() => (view.value?.sets ?? SETS.map((each) => each.id)).filter(isSet));

const recent = computed(() =>
  (view.value?.recent ?? []).slice(0, RECENT_SHOWN).map((roll) => ({
    key: `${roll.at}-${roll.total}-${roll.dice.length}`,
    asked: formatAsked(roll.dice, t.value),
    dice: formatDice(roll.dice, t.value),
    total: roll.total,
    reason: roll.reason,
    at: new Intl.DateTimeFormat(locale.value, { timeStyle: "short" }).format(roll.at),
    natural: naturalOf(roll.dice),
    many: roll.dice.length > 1,
  })),
);

function naturalOf(dice) {
  if (dice.length !== 1 || dice[0].sides !== NATURAL_HIGH) return "";
  if (dice[0].value === NATURAL_HIGH) return "high";
  return dice[0].value === NATURAL_LOW ? "low" : "";
}

const dieLabel = (kind) => t.value[`die.${kind}`];

function say(text, kind = "") {
  banner.value = { text, kind };
}

function sayResult(rolledCounts, values, reason) {
  const dice = toDice(rolledCounts, values);
  const sum = sumDice(dice);
  const natural = naturalOf(dice);
  if (reason) say(fill(t.value.rolled_for, { reason, asked: formatAsked(dice, t.value), total: sum }));
  else if (dice.length === 1) say(fill(t.value.rolled_one, { total: sum }), natural);
  else say(fill(t.value.rolled_many, { asked: formatAsked(dice, t.value), dice: formatDice(dice, t.value), total: sum }));
  if (natural) sound.flourish(natural === "high");
}

function showFlash(text) {
  flash.value = text;
  clearTimeout(flashTimer);
  flashTimer = setTimeout(() => (flash.value = ""), FLASH_MS);
}

function setTray(next, { show = true } = {}) {
  tray.value = next;
  storage.write(TRAY_KEY, JSON.stringify(next));
  if (show) board?.showTray(Tray.counts(next));
}

function add(kind) {
  const { tray: next, refused } = Tray.add(tray.value, kind);
  if (refused) {
    showFlash(fill(t.value.too_many, { max: maxDice.value }));
    return;
  }
  setTray(next);
}

const remove = (kind) => setTray(Tray.remove(tray.value, kind));
const clear = () => setTray(Tray.clear());

function pickSet(id) {
  if (!isSet(id) || id === set.value) return;
  set.value = id;
  props.bridge.chooseSet(id);
  board?.useSet(id, counts.value);
}

function toggleDark() {
  dark.value = !dark.value;
  storage.write(DARK_KEY, dark.value ? "1" : "0");
}

function toggleSound() {
  muted.value = sound.toggle();
}

function throwDice() {
  if (!open.value || busy.value || !board) return;
  const thrown = counts.value;
  pendingId = nextRequest++;
  busy.value = true;
  say(t.value.throwing);
  props.bridge.throwDice(pendingId, thrown);
  board.throwDice(pendingId, thrown);
}

function leave() {
  if (open.value) props.bridge.close();
}

// A view that came while the dice were in the air waits for them to land, so the list never tells the roll first.
function applyView(next) {
  if (busy.value) {
    pendingView = next;
    return;
  }
  view.value = next;
  const wanted = isSet(next.set) ? next.set : DEFAULT_SET;
  if (wanted !== set.value) {
    set.value = wanted;
    board?.useSet(wanted, counts.value);
  }
}

function settle() {
  busy.value = false;
  pendingId = 0;
  if (pendingView) {
    const next = pendingView;
    pendingView = null;
    applyView(next);
  }
}

async function stopPresenting() {
  clearTimeout(presentTimer);
  if (presenting.value === null) return;
  presenting.value = null;
  await nextTick();
  board?.setPresent(false);
  board?.showTray(counts.value);
  props.bridge.presented();
}

async function onState(next) {
  const opening = !open.value;
  if (presenting.value !== null) await stopPresenting();
  applyView(next);
  if (!opening) return;
  open.value = true;
  if (!busy.value) say(t.value.ready);
  await nextTick();
  board?.resize();
  board?.showTray(counts.value);
  throwButton.value?.$el.focus();
}

function onThrown(requestId, values) {
  if (requestId === pendingId) board?.arrive(requestId, values);
}

function onRefused(requestId, text) {
  if (requestId !== pendingId) return;
  board?.refuse(requestId);
  say(text, "warn");
  settle();
}

function onThrowOnBoard(asked) {
  if (!open.value) return;
  const next = Tray.KINDS.some((kind) => asked[kind] > 0) ? Tray.fromCounts(asked) : null;
  if (next) setTray(next, { show: false });
  throwDice();
}

async function onPresent(reason, presented, values) {
  if (!board || busy.value) return;
  const requestId = nextRequest++;
  pendingId = requestId;
  busy.value = true;
  presentReason = reason;
  say(fill(t.value.rolling_for, { reason }));
  if (open.value) {
    board.throwDice(requestId, presented);
  } else {
    clearTimeout(presentTimer);
    presenting.value = { reason };
    await nextTick();
    board.setPresent(true);
    board.throwDice(requestId, presented, board.pickRandomSets(Tray.total(presented)));
  }
  board.arrive(requestId, values);
}

function onLanded(landedCounts, values) {
  sayResult(landedCounts, values, presentReason);
  presentReason = "";
  settle();
  if (presenting.value !== null) presentTimer = setTimeout(stopPresenting, PRESENT_MS);
}

function onTimeout() {
  say(t.value.no_answer, "warn");
  presentReason = "";
  settle();
}

function onClosed() {
  open.value = false;
}

function moveCursor(by) {
  cursor.value = (cursor.value + by + KINDS.length) % KINDS.length;
}

function onKeydown(event) {
  if (!open.value || isComposing(event)) return;
  const code = readCode(event) || KEY_CODES[event.keyCode] || "";
  if (isHeldPress(event)) {
    if (code) event.preventDefault();
    return;
  }
  sound.wake();
  const kind = KINDS[cursor.value];
  if (code === "Space") throwDice();
  else if (ADDS[code]) add(ADDS[code]);
  else if (code === "Digit5") clear();
  else if (code === "ArrowUp") moveCursor(-1);
  else if (code === "ArrowDown") moveCursor(1);
  else if (code === "ArrowRight") add(kind);
  else if (code === "ArrowLeft") remove(kind);
  else if (code === "KeyV") toggleDark();
  else if (code === "KeyM") toggleSound();
  else if (code === "Escape") leave();
  else return;
  event.preventDefault();
}

const onResize = () => board?.resize();

onMounted(async () => {
  props.bridge.onState(onState);
  props.bridge.onThrown(onThrown);
  props.bridge.onRefused(onRefused);
  props.bridge.onThrowOnBoard(onThrowOnBoard);
  props.bridge.onPresent(onPresent);
  props.bridge.onClosed(onClosed);
  window.addEventListener("keydown", onKeydown);
  window.addEventListener("pointerdown", () => sound.wake());
  window.addEventListener("resize", onResize);
  props.bridge.signalReady();

  try {
    board = createBoard({ canvas: canvas.value, tags: tags.value, sound, stage: () => stage.value?.getBoundingClientRect() ?? null, onLanded, onTimeout });
    window.__diceBoard = board.probe;
    await board.start();
    await board.useSet(set.value, counts.value);
  } catch (error) {
    console.error("[CyrodiilRP] The dice board cannot be drawn.", error);
    board = null;
    failed.value = true;
  }
});

onUnmounted(() => {
  window.removeEventListener("keydown", onKeydown);
  window.removeEventListener("resize", onResize);
  clearTimeout(presentTimer);
  clearTimeout(flashTimer);
});
</script>

<template>
  <div class="dice" :class="{ 'dice--open': open, 'dice--present': presenting !== null && !open, 'dice--dark': dark, 'dice--shown': shown }">
    <canvas ref="canvas" class="dice__canvas" />
    <div ref="tags" class="dice__tags" aria-hidden="true" />

    <p v-if="presenting !== null && !open && banner" class="dice__caption" :class="banner.kind && `dice__caption--${banner.kind}`" role="status">
      {{ banner.text }}
    </p>

    <div v-if="open" class="dice__layout">
      <header class="dice__top">
        <h1 class="dice__title"><img class="dice__medallion" :src="d20" alt="">{{ t.title }}</h1>
        <p class="dice__banner" :class="banner?.kind && `dice__banner--${banner.kind}`" role="status" aria-live="polite">
          {{ failed ? t.no_board : banner?.text }}
          <span v-if="banner?.kind === 'high'" class="dice__natural">{{ t.natural_20 }}</span>
          <span v-else-if="banner?.kind === 'low'" class="dice__natural">{{ t.natural_1 }}</span>
        </p>
        <div class="dice__tools">
          <RpChip :pressed="dark" @click="toggleDark">{{ t.dark }}</RpChip>
          <RpChip :pressed="!muted" @click="toggleSound">{{ t.sound }}</RpChip>
          <RpButton class="dice__close" @click="leave">{{ t.close }}</RpButton>
        </div>
      </header>

      <RpPanel class="dice__panel dice__panel--tray" :title="t.tray">
        <ul class="dice__tiles">
          <li
            v-for="(kind, index) in KINDS"
            :key="kind"
            class="dice__tile"
            :class="{ 'dice__tile--cursor': index === cursor, 'dice__tile--used': tray[kind] > 0 && !tray.placeholder }"
            :data-kind="kind"
            @click="cursor = index"
          >
            <img class="dice__tile-icon" :src="iconOf(set, kind)" alt="">
            <span class="dice__tile-name">
              <b>{{ dieLabel(kind) }}</b>
              <small>{{ t[`die_name.${kind}`] }}</small>
            </span>
            <button type="button" class="dice__step" :aria-label="fill(t.fewer, { die: dieLabel(kind) })" :disabled="tray.placeholder || tray[kind] < 1" @click.stop="remove(kind)">
              &minus;
            </button>
            <span class="dice__tile-count" :aria-label="`${tray[kind]} ${dieLabel(kind)}`">{{ tray[kind] }}</span>
            <button type="button" class="dice__step" :aria-label="fill(t.more, { die: dieLabel(kind) })" @click.stop="add(kind)">+</button>
          </li>
        </ul>
        <p class="dice__asked">
          <b>{{ asked }}</b>
          <small :class="{ 'dice__warn': flash }" role="status">{{ countText }}</small>
        </p>
        <div class="dice__capacity" aria-hidden="true"><span :style="{ width: `${(total / maxDice) * 100}%` }" /></div>
        <RpButton class="dice__clear" @click="clear">{{ t.clear }}</RpButton>

        <h2 class="dice__heading">{{ t.sets }}</h2>
        <div class="dice__sets" role="group" :aria-label="t.sets">
          <RpChip v-for="id in sets" :key="id" class="dice__set" :pressed="id === set" :data-set="id" @click="pickSet(id)">
            <img class="dice__set-icon" :src="iconOf(id, 'd20')" alt="">
            <span>{{ t[`set.${id}`] }}</span>
          </RpChip>
        </div>
        <p class="dice__set-text">{{ t[`set_text.${set}`] }}</p>
      </RpPanel>

      <div ref="stage" class="dice__stage" />

      <RpPanel class="dice__panel dice__panel--journal" :title="t.recent">
        <p v-if="!recent.length" class="dice__note">{{ t.recent_none }}</p>
        <ol v-else class="dice__recent">
          <li v-for="roll in recent" :key="roll.key" class="dice__roll" :class="roll.natural && `dice__roll--${roll.natural}`">
            <span class="dice__roll-asked">
              {{ roll.asked }}
              <small v-if="roll.reason">{{ fill(t.recent_for, { reason: roll.reason }) }}</small>
              <small class="dice__roll-at">{{ roll.at }}</small>
            </span>
            <span class="dice__roll-total">{{ roll.total }}</span>
            <span v-if="roll.many" class="dice__roll-dice">{{ roll.dice }}</span>
          </li>
        </ol>

        <h2 class="dice__heading">{{ t.keys }}</h2>
        <dl class="dice__keys">
          <div><dt><kbd>Space</kbd></dt><dd>{{ t["key.throw"] }}</dd></div>
          <div><dt><kbd>1</kbd><kbd>2</kbd><kbd>3</kbd><kbd>4</kbd></dt><dd>{{ t["key.add"] }}</dd></div>
          <div><dt><kbd>5</kbd></dt><dd>{{ t["key.clear"] }}</dd></div>
          <div><dt><kbd>&uarr;</kbd><kbd>&darr;</kbd></dt><dd>{{ t["key.pick"] }}</dd></div>
          <div><dt><kbd>&larr;</kbd><kbd>&rarr;</kbd></dt><dd>{{ t["key.count"] }}</dd></div>
          <div><dt><kbd>V</kbd></dt><dd>{{ t["key.dark"] }}</dd></div>
          <div><dt><kbd>M</kbd></dt><dd>{{ t["key.sound"] }}</dd></div>
          <div><dt><kbd>Esc</kbd></dt><dd>{{ t["key.close"] }}</dd></div>
        </dl>
        <p class="dice__note">{{ t.hint }}</p>
      </RpPanel>

      <footer class="dice__bottom">
        <RpButton ref="throwButton" class="dice__throw" variant="primary" :disabled="busy || failed" @click="throwDice">
          <kbd>Space</kbd> {{ t.throw }}
        </RpButton>
      </footer>
    </div>
  </div>
</template>
