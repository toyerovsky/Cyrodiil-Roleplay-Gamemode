<script setup>
import { computed, nextTick, onMounted, ref, useTemplateRef } from "vue";
import { CHAT_HISTORY_LINES, CHAT_MAX_LENGTH } from "../shared/limits.js";
import { usePageTexts } from "../shared/locale.js";
import { isComposing, isHeldPress } from "../shared/press.js";

const props = defineProps({ bridge: { type: Object, required: true } });

// Up brings back so many of the lines the player sent.
const RECALL_LINES = 20;
const FADE_AFTER_MS = 8000;
// Of the log's height, so a page turn keeps a few lines of the one before in view.
const PAGE_TURN = 0.8;
// Within this of the end, new lines keep the log at its end.
const PINNED_PX = 4;

const { locale, t } = usePageTexts(props.bridge, "chat");
const clock = computed(() => new Intl.DateTimeFormat(locale.value, { hour: "2-digit", minute: "2-digit", hour12: false }));

const lines = ref([]);
const open = ref(false);
const idle = ref(false);
const draft = ref("");
const log = useTemplateRef("log");
const entry = useTemplateRef("entry");

const sent = [];
let recalled = -1;
let unsent = "";
let nextId = 0;
let fadeTimer = 0;

function wake() {
  idle.value = false;
  clearTimeout(fadeTimer);
  if (!open.value) fadeTimer = setTimeout(() => (idle.value = true), FADE_AFTER_MS);
}

const isAtEnd = () => !log.value || log.value.scrollHeight - log.value.scrollTop - log.value.clientHeight <= PINNED_PX;

function scrollToEnd() {
  if (log.value) log.value.scrollTop = log.value.scrollHeight;
}

// A player reading back keeps their place while others talk.
async function show(batch, replace) {
  const pinned = replace || isAtEnd();
  const added = batch.map((line) => ({ ...line, id: nextId++ }));
  lines.value = (replace ? added : [...lines.value, ...added]).slice(-CHAT_HISTORY_LINES);
  wake();
  await nextTick();
  if (pinned) scrollToEnd();
}

// The game hears K too and opens the chat a moment after the press, so that press's own character, and its repeats while
// K is still down, are not the player's text. A fresh press of K is.
const OPEN_KEY = "k";
let openingKeyDown = false;

// Open means focused: the game hands over the keyboard. An entry that cannot take it hears no Escape, so the keyboard
// goes straight back.
async function setOpen(value) {
  open.value = value;
  openingKeyDown = value;
  if (value) {
    wake();
    await nextTick();
    entry.value.focus();
    if (document.activeElement !== entry.value) props.bridge.close();
    return;
  }

  draft.value = "";
  recalled = -1;
  entry.value?.blur();
  wake();
  scrollToEnd();
}

// Enter sends without closing, so a conversation keeps the keyboard.
function submit() {
  const text = draft.value.trim();
  draft.value = "";
  recalled = -1;
  if (!text) return;
  if (sent.at(-1) !== text) sent.push(text);
  if (sent.length > RECALL_LINES) sent.shift();
  props.bridge.submit(text);
}

// Up goes back through what was sent, down comes forward to the line being typed.
async function recall(step) {
  const next = Math.min(Math.max(recalled + step, -1), sent.length - 1);
  if (next === recalled) return;
  if (recalled === -1) unsent = draft.value;
  recalled = next;
  draft.value = recalled === -1 ? unsent : sent[sent.length - 1 - recalled];
  await nextTick();
  entry.value.setSelectionRange(draft.value.length, draft.value.length);
}

const KEYS = {
  Escape: () => props.bridge.close(),
  Enter: submit,
  ArrowUp: () => recall(1),
  ArrowDown: () => recall(-1),
  PageUp: () => log.value?.scrollBy({ top: -log.value.clientHeight * PAGE_TURN }),
  PageDown: () => log.value?.scrollBy({ top: log.value.clientHeight * PAGE_TURN }),
  // The entry is all there is to focus: Tab would only lose the keyboard.
  Tab: () => {},
};

function onKeydown(event) {
  if (openingKeyDown && event.repeat && event.key.toLowerCase() === OPEN_KEY) {
    event.preventDefault();
    return;
  }
  openingKeyDown = false;
  const action = KEYS[event.key];
  if (!action || isComposing(event)) return;
  event.preventDefault();
  if (!isHeldPress(event)) action();
}

function onKeyup(event) {
  if (event.key.toLowerCase() === OPEN_KEY) openingKeyDown = false;
}

function onBeforeInput(event) {
  if (openingKeyDown && event.data?.toLowerCase() === OPEN_KEY) event.preventDefault();
}

// The keyboard went to something else of the game's, a form, which keeps it.
function onBlur() {
  if (open.value) props.bridge.reportBlur();
}

// The start of a command a wheel's option put in, for the player to finish.
async function setDraft(text) {
  draft.value = text;
  recalled = -1;
  await nextTick();
  entry.value.setSelectionRange(text.length, text.length);
}

onMounted(() => {
  props.bridge.onDraft(setDraft);
  props.bridge.onLines((batch) => show(batch, false));
  props.bridge.onHistory((batch) => show(batch, true));
  props.bridge.onOpen(setOpen);
  props.bridge.requestHistory();
  wake();
});
</script>

<template>
  <div class="chat" :class="{ 'chat--open': open, 'chat--idle': idle }">
    <ol v-show="lines.length" ref="log" class="chat__lines" role="log" :aria-label="t.log">
      <li v-for="line in lines" :key="line.id" class="chat-line" :class="`chat-line--${line.kind}`">
        <span class="chat-line__time">[{{ clock.format(new Date(line.at)) }}]</span>
        {{ line.text }}
      </li>
    </ol>
    <input
      ref="entry"
      v-model="draft"
      class="chat__entry"
      :maxlength="CHAT_MAX_LENGTH"
      autocomplete="off"
      spellcheck="false"
      :aria-label="t.entry"
      :placeholder="t.placeholder"
      @keydown="onKeydown"
      @keyup="onKeyup"
      @beforeinput="onBeforeInput"
      @blur="onBlur"
    >
  </div>
</template>
