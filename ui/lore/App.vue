<script setup>
import { computed, nextTick, onMounted, onUnmounted, reactive, ref, useTemplateRef, watch } from "vue";
import book from "../rp/art/lore-book.webp";
import { RpButton, RpChip, RpPanel } from "../rp/index.js";
import { fill, usePageTexts } from "../shared/locale.js";
import { isComposing, isHeldPress, readCode } from "../shared/press.js";
import { createAsker, PUSH_ID } from "./asker.js";
import { emptyValue, filterOptions, IDENTITY, knowsYou, NEARBY, OPTIONAL_NEED, otherVisibility, PARTS, PRIVATE, PUBLIC, traitState, visibilityOf } from "./card.js";
import CardView from "./CardView.vue";
import FieldDetail from "./FieldDetail.vue";
import FieldList from "./FieldList.vue";
import LorePicker from "./LorePicker.vue";
import NearbyDetail from "./NearbyDetail.vue";
import NearbyList from "./NearbyList.vue";
import TextEdit from "./TextEdit.vue";

const props = defineProps({ bridge: { type: Object, required: true } });

const { locale, t } = usePageTexts(props.bridge, "lore");

// Quiet after Space before a field's visibility goes to the server, so a few presses send one change.
const VIS_DELAY_MS = 450;
// A second Delete (or Esc over unsaved text) within this long confirms it.
const CONFIRM_MS = 2500;
// The dice fly on the dice board's small board this long before the number shows here.
const ROLL_REVEAL_MS = 4200;
const NEARBY_REFRESH_MS = 3000;
const SCROLL_STEP = 60;
const PAGE_STEP = 0.8;
const PICK_PAGE = 8;
const NAME_ROW = { id: "name", name: true };
// The game hands a page the Windows key code alone: the keys readCode does not know. End is never the page's: its bind
// opens and closes the card.
const KEY_CODES = { 8: "Backspace", 32: "Space", 46: "Delete", 107: "Equal", 109: "Minus", 187: "Equal", 189: "Minus" };
const TYPING = ["text", "long", "number"];

const asker = createAsker(props.bridge, { noAnswer: () => t.value.no_answer, badAnswer: () => t.value.bad_answer });

const open = ref(false);
const mode = ref("edit");
const ui = ref("browse");
const card = reactive({ loaded: false, fields: [], values: {}, raceHint: null, readOnly: false, used: 0, maxCard: 0, name: "", namePublic: false, header: "" });
const part = ref(0);
const row = ref(0);
const pick = ref(null);
const preview = ref(null);
const previewLevel = ref(PUBLIC);
const viewer = ref(null);
const nearby = reactive({ loaded: false, players: [], range: null });
const status = ref({ text: "", kind: "info" });
const confirm = ref(null);
const rolling = ref(null);
const draft = ref("");
const draftError = ref("");
const busy = ref(false);

const list = useTemplateRef("list");
const editor = useTemplateRef("editor");
const picker = useTemplateRef("picker");
const detail = useTemplateRef("detail");
const viewerBody = useTemplateRef("viewerBody");

const visTimers = new Map();
let nearbyTimer = 0;

const partId = computed(() => PARTS[part.value]);
const onNearby = computed(() => partId.value === NEARBY);
const fieldsOf = (id) => card.fields.filter((field) => field.part === id);
const fieldById = (id) => card.fields.find((field) => field.id === id);
const valueOf = (field) => card.values[field.id] ?? emptyValue(field);
const rows = computed(() => {
  if (onNearby.value) return nearby.players;
  const fields = fieldsOf(partId.value);
  return partId.value === IDENTITY ? [NAME_ROW, ...fields] : fields;
});
const current = computed(() => rows.value[row.value] ?? null);
const currentField = computed(() => (!onNearby.value && current.value && !current.value.name ? current.value : null));
const person = computed(() => (onNearby.value ? current.value : null));
const neededField = computed(() => {
  const field = currentField.value;
  if (!field?.needs || field.needs === OPTIONAL_NEED) return null;
  const needed = fieldById(field.needs);
  return needed && valueOf(needed).empty ? needed : null;
});
const shownOptions = computed(() => (pick.value ? filterOptions(pick.value.options, pick.value.query, (kind) => t.value[`kind.${kind}`]) : []));
const sections = computed(() =>
  PARTS.map((id, index) => ({
    id,
    index,
    count: id === NEARBY ? nearby.players.filter((each) => each.incoming).length : fieldsOf(id).filter((field) => !valueOf(field).empty).length,
  })),
);
const usedShare = computed(() => (card.maxCard ? Math.min(100, (card.used / card.maxCard) * 100) : 0));
// A text box or the picker's search holds the keyboard: the game leaves End to its caret (LoreOverlay.cs).
const typing = computed(() => open.value && mode.value === "edit" && (TYPING.includes(ui.value) || ui.value === "pick"));
watch(typing, (isTyping) => props.bridge.typing(isTyping));
const confirming = (what, id) => confirm.value?.what === what && confirm.value.id === id;

function setStatus(text, kind = "info") {
  status.value = { text: text ?? "", kind };
}

async function focusList() {
  await nextTick();
  list.value?.focus();
}

async function focusEditor() {
  await nextTick();
  editor.value?.focus();
}

// --- requests -------------------------------------------------------------------------------------------------------

async function load() {
  setStatus(card.loaded ? "" : t.value.opening, "busy");
  const answer = await asker.ask({ op: "load" });
  if (answer.dropped) return;
  if (!answer.ok) {
    setStatus(answer.message, "error");
    return;
  }
  const keep = card.loaded ? currentField.value?.id : null;
  Object.assign(card, {
    loaded: true,
    fields: answer.fields ?? [],
    values: answer.card ?? {},
    raceHint: answer.raceHint ?? null,
    readOnly: !!answer.readOnly,
    used: answer.used ?? 0,
    maxCard: answer.maxCard ?? 0,
    name: answer.name ?? "",
    namePublic: !!answer.namePublic,
    header: answer.header ?? "",
  });
  if (keep) moveTo(keep);
  if (row.value >= rows.value.length) row.value = 0;
  setStatus(answer.readOnly ? t.value.read_only : "", answer.readOnly ? "error" : "info");
  if (ui.value === "browse") focusList();
}

function apply(answer) {
  if (answer.card) card.values = answer.card;
  if (typeof answer.used === "number") card.used = answer.used;
  if (typeof answer.namePublic === "boolean") card.namePublic = answer.namePublic;
  if (typeof answer.header === "string") card.header = answer.header;
  setStatus(answer.message || (answer.ok ? "" : t.value.failed), answer.ok ? "done" : "error");
}

async function change(request, quiet = false) {
  if (!quiet) setStatus(t.value.saving, "busy");
  busy.value = true;
  const answer = await asker.ask(request);
  busy.value = false;
  if (!answer.dropped) apply(answer);
  return answer;
}

// --- browsing -------------------------------------------------------------------------------------------------------

function moveTo(id) {
  const field = fieldById(id);
  if (!field) return;
  part.value = PARTS.indexOf(field.part);
  row.value = rows.value.findIndex((each) => each.id === id);
}

function selectRow(index) {
  if (!mayLeaveEditing()) return;
  row.value = index;
  confirm.value = null;
  if (ui.value !== "browse") back();
  else setStatus("");
  focusList();
}

function moveRow(by) {
  const count = Math.max(1, rows.value.length);
  selectRow((row.value + by + count) % count);
}

function selectPart(index) {
  if (!mayLeaveEditing()) return;
  part.value = index;
  row.value = 0;
  confirm.value = null;
  if (ui.value !== "browse") back();
  setStatus("");
  if (onNearby.value) loadNearby();
  focusList();
}

const movePart = (by) => selectPart((part.value + by + PARTS.length) % PARTS.length);

function back(message) {
  ui.value = "browse";
  pick.value = null;
  preview.value = null;
  confirm.value = null;
  draftError.value = "";
  if (message !== undefined) setStatus(message, message ? "done" : "info");
  focusList();
}

function refuseReadOnly() {
  if (!card.readOnly) return false;
  setStatus(t.value.read_only, "error");
  return true;
}

function openRow(index) {
  row.value = index;
  if (onNearby.value) readCard(person.value);
  else startEdit();
}

// --- editing --------------------------------------------------------------------------------------------------------

function startEdit() {
  if (current.value?.name) {
    cycleVisibility();
    return;
  }
  const field = currentField.value;
  if (!field || refuseReadOnly()) return;
  if (field.type === "catalog" || field.type === "choice") {
    openPicker(field);
    return;
  }
  if (field.type === "trait") {
    const state = traitState(valueOf(field));
    if (state === "rolled") return keepTrait(field);
    if (state === "fixed") return setStatus(fill(t.value["trait.set_for_good"], { label: field.label }), "warn");
  }
  setStatus("");
  confirm.value = null;
  draftError.value = "";
  ui.value = field.type === "longText" ? "long" : field.type === "trait" ? "number" : "text";
  draft.value = field.type === "trait" ? "" : (valueOf(field).text ?? "");
  focusEditor();
}

async function saveText() {
  const field = currentField.value;
  const text = draft.value.trim();
  let answer;
  if (ui.value === "number") {
    if (!text) return back("");
    answer = await change({ op: "set", field: field.id, text });
  } else {
    if (text === (valueOf(field).text ?? "").trim()) return back("");
    answer = await change(text ? { op: "set", field: field.id, text } : { op: "clear", field: field.id });
  }
  if (answer.dropped) return;
  if (answer.ok) back(answer.message);
  else {
    // The box says why, so the footer does not say it twice.
    draftError.value = answer.message || t.value.failed;
    setStatus("");
    focusEditor();
  }
}

// Leaving a field being written, by Esc or by a click elsewhere, asks first when it would throw words away.
function mayLeaveEditing() {
  if (!TYPING.includes(ui.value)) return true;
  const field = currentField.value;
  const dirty = ui.value !== "number" && draft.value.trim() !== (valueOf(field).text ?? "").trim();
  if (!dirty || isConfirmed("discard", field.id)) return true;
  setStatus(ui.value === "long" ? t.value.discard_long : t.value.discard, "warn");
  return false;
}

function cancelText() {
  if (mayLeaveEditing()) back("");
}

// The first press asks, a second one within a moment does it.
function isConfirmed(what, id) {
  const asked = confirm.value;
  if (asked?.what === what && asked.id === id && Date.now() - asked.at < CONFIRM_MS) {
    confirm.value = null;
    return true;
  }
  confirm.value = { what, id, at: Date.now() };
  return false;
}

function sendLater(key, request, delay) {
  clearTimeout(visTimers.get(key)?.timer);
  const send = () => {
    visTimers.delete(key);
    change(request, true);
  };
  if (delay) visTimers.set(key, { timer: setTimeout(send, delay), send });
  else send();
}

function flushVisibility() {
  for (const { timer, send } of [...visTimers.values()]) {
    clearTimeout(timer);
    send();
  }
}

function setVisibility(field, vis, delay = 0) {
  if (refuseReadOnly()) return;
  if (field.locked) {
    const locked = visibilityOf(field, valueOf(field));
    setStatus(fill(t.value.always, { label: field.label, vis: t.value[`vis.${locked}`] }), "warn");
    return;
  }
  card.values = { ...card.values, [field.id]: { ...valueOf(field), vis } };
  setStatus(`${field.label}: ${t.value[`vis_help.${vis}`]}`);
  sendLater(field.id, { op: "vis", field: field.id, vis }, delay);
}

function setNameVisibility(isPublic, delay = 0) {
  if (refuseReadOnly()) return;
  card.namePublic = isPublic;
  setStatus(t.value[`vis_help.${isPublic ? "name_public" : "name_private"}`]);
  sendLater("name", { op: "namevis", vis: isPublic ? PUBLIC : PRIVATE }, delay);
}

function cycleVisibility() {
  if (current.value?.name) setNameVisibility(!card.namePublic, VIS_DELAY_MS);
  else if (currentField.value) setVisibility(currentField.value, otherVisibility(valueOf(currentField.value).vis ?? currentField.value.vis), VIS_DELAY_MS);
}

function clearField() {
  const field = currentField.value;
  if (!field || valueOf(field).empty || refuseReadOnly()) return;
  if (field.type === "trait") {
    setStatus(fill(t.value["trait.no_clear"], { label: field.label }), "warn");
    return;
  }
  if (isConfirmed("clear", field.id)) change({ op: "clear", field: field.id });
  else setStatus(fill(t.value.clear_confirm, { label: field.label }), "warn");
}

const choose = (field, id) => !refuseReadOnly() && change({ op: "set", field: field.id, text: id });

// --- traits: rolled 5d20 on the server, the dice shown on the dice board's small board, one reroll ----------------

const wait = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

async function rollTrait() {
  const field = currentField.value;
  if (field?.type !== "trait" || rolling.value || refuseReadOnly()) return;
  const state = traitState(valueOf(field));
  if (state === "fixed") {
    setStatus(fill(t.value["trait.no_more_rolls"], { label: field.label }), "warn");
    return;
  }
  rolling.value = field.id;
  setStatus(fill(t.value[state === "rolled" ? "trait.rerolling" : "trait.rolling"], { label: field.label }), "busy");
  const asked = Date.now();
  const answer = await asker.ask({ op: "roll", field: field.id });
  if (answer.dropped) return;
  if (answer.ok && answer.roll) await wait(Math.max(0, ROLL_REVEAL_MS - (Date.now() - asked)));
  rolling.value = null;
  apply(answer);
}

const keepTrait = (field) => change({ op: "keep", field: field.id });

// --- the picker -----------------------------------------------------------------------------------------------------

async function openPicker(field) {
  if (neededField.value) {
    setStatus(fill(t.value.needs_first, { field: neededField.value.label }), "warn");
    return;
  }
  setStatus("");
  ui.value = "pick";
  pick.value = { field, options: [], query: "", index: 0, loading: field.type === "catalog", message: "" };
  await nextTick();
  picker.value?.focus();
  if (field.type === "choice") {
    pick.value.options = field.choices ?? [];
  } else {
    const answer = await asker.ask({ op: "options", field: field.id });
    if (ui.value !== "pick" || pick.value?.field !== field) return;
    pick.value.loading = false;
    pick.value.options = answer.options ?? [];
    pick.value.message = answer.message || (answer.ok ? "" : t.value.list_failed);
  }
  const value = valueOf(field);
  const wanted = field.type === "choice" ? value.text : (value.catalogId ?? (field.id === "race" ? card.raceHint?.id : null));
  pick.value.index = Math.max(0, shownOptions.value.findIndex((option) => option.id === wanted));
}

function setQuery(query) {
  pick.value.query = query;
  pick.value.index = 0;
}

function movePick(by) {
  const count = shownOptions.value.length;
  if (count) pick.value.index = Math.max(0, Math.min(count - 1, pick.value.index + by));
}

async function pickOption(index = pick.value.index) {
  const { field } = pick.value;
  const option = shownOptions.value[index];
  if (!option || busy.value) return;
  pick.value.index = index;
  const answer = await change(field.type === "choice" ? { op: "set", field: field.id, text: option.id } : { op: "pick", field: field.id, id: option.id });
  if (answer.ok) back(answer.message);
}

const pickedId = computed(() => {
  if (!pick.value) return null;
  const value = valueOf(pick.value.field);
  return pick.value.field.type === "choice" ? (value.text ?? null) : (value.catalogId ?? null);
});

// --- preview: the card as a stranger sees it, cut by the server -------------------------------------------------------

// As a stranger (public) or as someone the player introduced themselves to (private).
async function showPreview(level = PUBLIC) {
  if (!card.loaded) return;
  ui.value = "preview";
  previewLevel.value = level;
  preview.value = null;
  confirm.value = null;
  focusList();
  const answer = await asker.ask({ op: "preview", level });
  if (ui.value !== "preview" || previewLevel.value !== level || answer.dropped) return;
  preview.value = answer.ok ? answer.view : { error: answer.message || t.value.failed };
}

// --- nearby ---------------------------------------------------------------------------------------------------------

async function loadNearby() {
  const answer = await asker.ask({ op: "nearby" });
  if (answer.dropped) return;
  if (!answer.ok) {
    if (onNearby.value) setStatus(answer.message, "error");
    return;
  }
  const keep = person.value?.id;
  nearby.players = answer.players ?? [];
  nearby.range = answer.range ?? null;
  nearby.loaded = true;
  if (!onNearby.value) return;
  const at = nearby.players.findIndex((each) => each.id === keep);
  row.value = at >= 0 ? at : Math.min(row.value, Math.max(0, nearby.players.length - 1));
}

async function social(op, target) {
  if (!target || busy.value) return;
  setStatus(t.value.asking, "busy");
  busy.value = true;
  const answer = await asker.ask({ op, player: String(target.id) });
  busy.value = false;
  if (answer.dropped) return;
  confirm.value = null;
  setStatus(answer.message || (answer.ok ? "" : t.value.failed), answer.ok ? "done" : "error");
  await loadNearby();
}

function introduceOrAccept(target) {
  if (!target) return;
  social(target.incoming ? "accept" : "introduce", target);
}

function decline(target) {
  if (target?.incoming) social("decline", target);
}

function forget(target) {
  if (!target || !knowsYou(target)) return;
  if (isConfirmed("forget", target.id)) social("forget", target);
  else setStatus(fill(t.value["nearby.forget_confirm"], { name: target.header }), "warn");
}

async function readCard(target) {
  if (!target) return;
  const answer = await asker.ask({ op: "view", player: String(target.id) });
  if (answer.dropped) return;
  if (!answer.ok) {
    setStatus(answer.message || t.value.failed, "error");
    return;
  }
  showViewer(answer, NEARBY);
}

// --- another player's card ------------------------------------------------------------------------------------------

async function showViewer(answer, from) {
  mode.value = "view";
  viewer.value = { id: answer.id ?? null, who: answer.who || t.value.someone, relation: answer.relation ?? "", view: answer.view ?? null, from };
  await nextTick();
  if (viewerBody.value) {
    viewerBody.value.scrollTop = 0;
    viewerBody.value.focus();
  }
}

function leaveViewer() {
  if (viewer.value?.from === NEARBY) {
    mode.value = "edit";
    viewer.value = null;
    focusList();
  } else leave();
}

// --- the game -------------------------------------------------------------------------------------------------------

function onPush(json) {
  let push;
  try {
    push = JSON.parse(json);
  } catch {
    return;
  }
  open.value = true;
  if (push.op === "view") {
    showViewer(push, null);
    return;
  }
  mode.value = "edit";
  viewer.value = null;
  if (ui.value === "pick" || ui.value === "preview") back();
  load();
}

function onAnswer(requestId, json) {
  if (requestId === PUSH_ID) onPush(json);
  else asker.answer(requestId, json);
}

function onClosed() {
  open.value = false;
  asker.drop();
  for (const { timer } of visTimers.values()) clearTimeout(timer);
  visTimers.clear();
  ui.value = "browse";
  mode.value = "edit";
  pick.value = null;
  preview.value = null;
  viewer.value = null;
  confirm.value = null;
  rolling.value = null;
  busy.value = false;
  draftError.value = "";
  setStatus("");
}

function leave() {
  if (!open.value) return;
  flushVisibility();
  props.bridge.close();
}

function scrollBy(element, code) {
  if (!element) return;
  const top = { ArrowDown: SCROLL_STEP, ArrowUp: -SCROLL_STEP, PageDown: element.clientHeight * PAGE_STEP, PageUp: -element.clientHeight * PAGE_STEP }[code];
  element.scrollBy?.({ top });
}

// --- keys -----------------------------------------------------------------------------------------------------------

const SECTION_STEP = { ArrowLeft: -1, KeyQ: -1, ArrowRight: 1, KeyE: 1 };

function viewerKey(code) {
  if (code === "Escape") leaveViewer();
  else if (["ArrowDown", "ArrowUp", "PageDown", "PageUp"].includes(code)) scrollBy(viewerBody.value, code);
  else return false;
  return true;
}

function typingKey(code, event) {
  if (code === "Escape") cancelText();
  else if (code === "Enter" && (ui.value !== "long" || event.ctrlKey)) saveText();
  else return false;
  return true;
}

function pickKey(code) {
  const steps = { ArrowDown: 1, ArrowUp: -1, PageDown: PICK_PAGE, PageUp: -PICK_PAGE };
  if (code === "Escape") back("");
  else if (code in steps) movePick(steps[code]);
  else if (code === "Enter") pickOption();
  else return false;
  return true;
}

function nearbyKey(code) {
  const target = person.value;
  if (code === "Enter") readCard(target);
  else if (code === "Equal") introduceOrAccept(target);
  else if (code === "Minus") decline(target);
  else if (code === "Delete" || code === "Backspace") forget(target);
  else return false;
  return true;
}

function browseKey(code) {
  if (code === "Escape") leave();
  else if (!card.loaded) return false;
  else if (rolling.value) return true;
  else if (code === "ArrowDown") moveRow(1);
  else if (code === "ArrowUp") moveRow(-1);
  else if (code in SECTION_STEP) movePart(SECTION_STEP[code]);
  else if (code === "PageDown" || code === "PageUp") scrollBy(detail.value, code);
  else if (onNearby.value) return nearbyKey(code);
  else if (code === "Enter") startEdit();
  else if (code === "Space") cycleVisibility();
  else if (code === "Delete" || code === "Backspace") clearField();
  else if (code === "KeyR" && currentField.value?.type === "trait") rollTrait();
  else if (code === "KeyP") showPreview(PUBLIC);
  else return false;
  return true;
}

function previewKey(code) {
  if (code === "Escape") back("");
  else if (["KeyP", "ArrowRight", "ArrowLeft"].includes(code)) showPreview(otherVisibility(previewLevel.value));
  else if (["ArrowDown", "ArrowUp", "PageDown", "PageUp"].includes(code)) scrollBy(detail.value, code);
  else return false;
  return true;
}

function onKeydown(event) {
  if (!open.value || isComposing(event)) return;
  const code = readCode(event) || KEY_CODES[event.keyCode] || "";
  if (isHeldPress(event)) {
    if (code && !TYPING.includes(ui.value)) event.preventDefault();
    return;
  }
  // A button with the focus answers Enter and Space itself, as a click.
  if (event.target?.tagName === "BUTTON" && (code === "Enter" || code === "Space")) return;

  let handled;
  if (mode.value === "view") handled = viewerKey(code);
  else if (TYPING.includes(ui.value)) handled = typingKey(code, event);
  else if (ui.value === "pick") handled = pickKey(code);
  else if (ui.value === "preview") handled = previewKey(code);
  else handled = browseKey(code);
  if (handled) event.preventDefault();
}

// --- what the footer says the keys do ------------------------------------------------------------------------------

const ARROWS_UP_DOWN = "↑↓";
const ARROWS_SIDES = "←→";
const hints = computed(() => {
  const k = (cap, key) => ({ cap, text: t.value[`key.${key}`] });
  if (mode.value === "view") return [k(ARROWS_UP_DOWN, "scroll"), k("Esc", viewer.value?.from === NEARBY ? "back" : "close")];
  if (ui.value === "text") return [k("Enter", "save"), k("Esc", "cancel")];
  if (ui.value === "number") return [k("Enter", "apply"), k("Esc", "cancel")];
  if (ui.value === "long") return [k("Ctrl+Enter", "save"), k("Enter", "new_line"), k("Esc", "cancel")];
  if (ui.value === "pick") return [k(ARROWS_UP_DOWN, "choose"), k("Enter", "pick"), k("Esc", "back")];
  if (ui.value === "preview") return [k("P", "other_viewer"), k(ARROWS_UP_DOWN, "scroll"), k("Esc", "back")];
  if (onNearby.value) return [k(ARROWS_UP_DOWN, "player"), k(ARROWS_SIDES, "section"), k("Enter", "read"), k("=", "introduce"), k("-", "decline"), k("Del", "forget"), k("Esc", "close")];
  if (currentField.value?.type === "trait")
    return [k(ARROWS_UP_DOWN, "trait"), k(ARROWS_SIDES, "section"), k("R", "roll"), k("Enter", "type_keep"), k("Space", "vis"), k("P", "preview"), k("Esc", "close")];
  return [k(ARROWS_UP_DOWN, "field"), k(ARROWS_SIDES, "section"), k("Enter", "edit"), k("Space", "vis"), k("Del", "clear"), k("P", "preview"), k("Esc", "close")];
});

const title = computed(() => (mode.value === "view" ? (viewer.value?.who ?? "") : t.value.title));

onMounted(() => {
  props.bridge.onAnswer(onAnswer);
  props.bridge.onClosed(onClosed);
  window.addEventListener("keydown", onKeydown);
  nearbyTimer = setInterval(() => {
    if (open.value && mode.value === "edit" && ui.value === "browse" && onNearby.value) loadNearby();
  }, NEARBY_REFRESH_MS);
  props.bridge.signalReady();
});

onUnmounted(() => {
  window.removeEventListener("keydown", onKeydown);
  clearInterval(nearbyTimer);
  for (const { timer } of visTimers.values()) clearTimeout(timer);
});
</script>

<template>
  <div v-if="open" class="lore" :class="`lore--${mode}`">
    <div class="lore__document">
      <img class="lore__medallion" :src="book" alt="">
      <RpPanel class="lore__panel" variant="letter" :title="title">
        <!-- Another player's card, as they let this player see it. -->
        <template v-if="mode === 'view'">
          <p class="lore__subtitle">{{ t.viewer_lead }}</p>
          <div ref="viewerBody" class="lore__viewer" tabindex="0" :aria-label="t.viewer_lead">
            <CardView v-if="viewer?.view" :view="viewer.view" :words="t" :heading-level="2" :show-header="viewer.view.header !== viewer.who" />
            <p v-else class="lore-view__none">{{ fill(t.viewer_none, { who: viewer?.who ?? "" }) }}</p>
          </div>
        </template>

        <!-- The player's own card. -->
        <template v-else>
          <div class="lore__head">
            <p class="lore__subtitle">
              <span class="lore__name">{{ card.name }}</span>
              <span v-if="card.header && !card.namePublic" class="lore__seen">{{ fill(t.seen_as, { header: card.header }) }}</span>
            </p>
            <div v-if="card.loaded" class="lore__usage">
              <span class="lore__usage-bar" aria-hidden="true"><span :style="{ width: `${usedShare}%` }" /></span>
              <small>{{ fill(t.used, { used: card.used.toLocaleString(locale), max: card.maxCard.toLocaleString(locale) }) }}</small>
            </div>
          </div>

          <div class="lore__sections" role="tablist" :aria-label="t.sections">
            <button
              v-for="section in sections"
              :key="section.id"
              type="button"
              role="tab"
              class="lore__section"
              :class="{ 'lore__section--on': section.index === part, 'lore__section--alert': section.id === 'nearby' && section.count }"
              :aria-selected="section.index === part"
              :tabindex="section.index === part ? 0 : -1"
              :data-part="section.id"
              @click="selectPart(section.index)"
            >
              {{ t[`part.${section.id}`] }}<small v-if="section.count" class="lore__count">{{ section.count }}</small>
            </button>
          </div>

          <div class="lore__body" :aria-busy="!card.loaded">
            <NearbyList
              v-if="onNearby"
              ref="list"
              :players="nearby.players"
              :loaded="nearby.loaded"
              :cursor="row"
              :label="t['part.nearby']"
              :t="t"
              @select="selectRow"
              @open="openRow"
            />
            <FieldList
              v-else
              ref="list"
              :rows="rows"
              :cursor="row"
              :values="card.values"
              :name="card.name"
              :name-public="card.namePublic"
              :label="t[`part.${partId}`]"
              :t="t"
              @select="selectRow"
              @open="openRow"
            />

            <div ref="detail" class="lore__detail">
              <template v-if="!card.loaded" />
              <div v-else-if="ui === 'preview'" class="lore-detail__field">
                <h2 class="lore-detail__title">{{ t[`preview.title_${previewLevel}`] }}</h2>
                <div class="lore-who" role="group" :aria-label="t['preview.as']">
                  <RpChip
                    v-for="level in [PUBLIC, PRIVATE]"
                    :key="level"
                    :pressed="previewLevel === level"
                    :data-viewer="level"
                    @click="showPreview(level)"
                  >
                    {{ t[`preview.as_${level}`] }}
                  </RpChip>
                </div>
                <p class="lore-detail__kind">{{ t[`preview.lead_${previewLevel}`] }}</p>
                <p v-if="!preview" class="lore-detail__none" role="status">{{ t.asking }}</p>
                <p v-else-if="preview.error" class="lore-detail__none" role="alert">{{ preview.error }}</p>
                <CardView v-else :view="preview" :words="t" class="lore__seen-card" />
                <div class="lore-detail__actions">
                  <RpButton data-action="back" @click="back('')">{{ t.back }}</RpButton>
                </div>
              </div>
              <NearbyDetail
                v-else-if="onNearby"
                :person="person"
                :loaded="nearby.loaded"
                :range="nearby.range"
                :confirming-forget="!!person && confirming('forget', person.id)"
                :busy="busy"
                :t="t"
                @read="readCard(person)"
                @introduce="introduceOrAccept(person)"
                @accept="introduceOrAccept(person)"
                @decline="decline(person)"
                @forget="forget(person)"
              />
              <template v-else-if="current">
                <div v-if="ui === 'pick' && pick" class="lore-detail__field lore-detail__field--fill">
                  <h2 class="lore-detail__title">{{ pick.field.label }}</h2>
                  <p class="lore-detail__kind">{{ fill(t[`kind_line.${pick.field.type}`], { count: pick.field.choices?.length ?? 0 }) }}</p>
                  <LorePicker
                    ref="picker"
                    :query="pick.query"
                    :field="pick.field"
                    :options="shownOptions"
                    :index="pick.index"
                    :loading="pick.loading"
                    :message="pick.message"
                    :chosen="pickedId"
                    :game-race="card.raceHint?.id ?? null"
                    :busy="busy"
                    :t="t"
                    @update:query="setQuery"
                    @move="(index) => (pick.index = index)"
                    @pick="pickOption"
                    @back="back('')"
                  />
                </div>
                <div v-else-if="ui !== 'browse' && currentField" class="lore-detail__field">
                  <h2 class="lore-detail__title">{{ currentField.label }}</h2>
                  <p class="lore-detail__hint">{{ currentField.hint }}</p>
                  <TextEdit ref="editor" v-model="draft" :field="currentField" :kind="ui" :error="draftError" :busy="busy" :t="t" @save="saveText" @cancel="cancelText" />
                </div>
                <FieldDetail
                  v-else
                  :row="current"
                  :value="currentField ? valueOf(currentField) : {}"
                  :t="t"
                  :name="card.name"
                  :name-public="card.namePublic"
                  :header="card.header"
                  :race-hint="card.raceHint"
                  :needs="neededField"
                  :read-only="card.readOnly"
                  :rolling="rolling === current.id"
                  :confirming-clear="confirming('clear', current.id)"
                  :busy="busy"
                  @vis="(vis) => setVisibility(currentField, vis)"
                  @namevis="(isPublic) => setNameVisibility(isPublic)"
                  @edit="startEdit"
                  @choose="(id) => choose(currentField, id)"
                  @clear="clearField"
                  @roll="rollTrait"
                  @keep="keepTrait(currentField)"
                />
              </template>
            </div>
          </div>
        </template>

        <div class="lore__footer">
          <p class="lore__status" :class="`lore__status--${status.kind}`" role="status" aria-live="polite">{{ status.text }}</p>
          <p class="lore__keys">
            <span v-for="hint in hints" :key="hint.cap + hint.text" class="lore__key"><kbd>{{ hint.cap }}</kbd>{{ hint.text }}</span>
          </p>
          <div class="lore__buttons">
            <RpButton v-if="mode === 'view' && viewer?.from === 'nearby'" data-action="back-nearby" @click="leaveViewer">{{ t.back_nearby }}</RpButton>
            <RpButton v-if="mode === 'edit' && card.loaded && ui === 'browse'" data-action="preview" @click="showPreview(PUBLIC)">{{ t.preview }}</RpButton>
            <RpButton class="lore__close" data-action="close" @click="leave">{{ t.close }}</RpButton>
          </div>
        </div>
      </RpPanel>
    </div>
  </div>
</template>
