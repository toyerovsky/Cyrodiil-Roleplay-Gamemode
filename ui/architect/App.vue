<script setup>
import { computed, onMounted, onUnmounted, reactive, ref, useId, watch } from "vue";
import character from "../rp/art/icons/character.webp";
import items from "../rp/art/icons/items.webp";
import keys from "../rp/art/icons/keys.webp";
import lore from "../rp/art/icons/lore.webp";
import world from "../rp/art/icons/world.webp";
import { makeTabId, RpButton, RpChip, RpField, RpPanel, RpTabs } from "../rp/index.js";
import { fill, usePageTexts } from "../shared/locale.js";
import { isComposing, isHeldPress, readCode } from "../shared/press.js";

const props = defineProps({ bridge: { type: Object, required: true } });

const FINE_TURN_DEGREES = 1;
// The panel's choices: steps of the grid a tile (0 for none), and how far one turn goes.
const STEPS = [1, 2, 4, 0];
const TURNS = [90, 45, 15, 5];
const HEIGHTS = [1, 5, 10, 25, 50, 100];
const FINE_RAISE_CENTIMETRES = 1;
// How far the mouse moves with its button down before a picked piece is carried: less is a click.
const DRAG_PIXELS = 6;
const RIGHT_BUTTON = 2;
// WASD move the view while held, or the picked piece a step a press: forward and right.
const PAN_KEYS = { KeyW: [1, 0], KeyS: [-1, 0], KeyA: [0, -1], KeyD: [0, 1] };
const MIDDLE = 0.5;
const CENTIMETRES_PER_METRE = 100;
const NO_FIT = "None";

// A category's medallion until the game has drawn the picture of its first piece.
const MEDALLIONS = { Houses: world, Kits: world, Floors: world, Farm: items, Fences: keys, Doors: keys, Furniture: character, Decoration: lore };
const KITS = "Kits";

const { t } = usePageTexts(props.bridge, "architect");

// Architect mode as the server last sent it, null while it is closed.
const view = ref(null);
// The drawer's pieces: [{key, name, category, use, kit}], sent apart from the view as they are many.
const pieces = ref([]);
const looking = ref(false);
const fit = ref(NO_FIT);
const refused = ref(null);
const answer = ref(null);
const selected = ref("");
const marked = ref({ width: 0, depth: 0 });
const step = ref(1);
const turnStep = ref(15);
const heightStep = ref(10);
// A carried piece settles on what is under it (true) or stands on the plot's ground.
const settle = ref(true);
const category = ref(null);
// The kit the Kits tab shows, null for the first.
const kit = ref(null);
const saving = ref(false);
// What the form's check refused: {field, text}, the field being "price", "weeklyTax", "height" or "size".
const problem = ref(null);
const form = reactive({ kind: "Residential", price: "", weeklyTax: "", height: "" });
// The catalogue over the whole screen: open as building starts, gone while a piece is dragged out of it, C brings it back.
const catalogue = ref(true);
// Each piece's picture as the game drew it, a data URL by the piece's key.
const pictures = reactive({});
const panelId = useId();

// What the mouse carries: {piece}, the key of a drawer's piece or "" for a point of the world. Null when nothing.
let drag = null;
let lastPoint = null;
// Where the button went down, and whether the mouse has gone far enough from there to carry.
let downAt = null;
let carrying = false;
const panning = new Set();
// The right mouse button turning the view: where it was last, and the pixels not sent yet.
let orbitAt = null;
let orbitBy = { across: 0, down: 0 };
let orbitFrame = 0;
let frame = 0;
// Where the mouse last was over the world, for Ctrl+V; and whether Z is held, which turns W and S up and down.
let hoverAt = null;
let heightHeld = false;
// The name of the piece Ctrl+C copied, empty for none.
const copied = ref("");
// The plot a Remove asks about: {id, pieces}, null while none.
const removing = ref(null);
const removeId = useId();

const building = computed(() => view.value?.tool === "Build");
const marking = computed(() => view.value?.tool === "Mark");
const choosing = computed(() => view.value?.tool === "Choose");
const editing = computed(() => building.value && !!selected.value);
const shownCategory = computed({
  get: () => category.value ?? view.value?.categories[0],
  set: (name) => (category.value = name),
});
const tabs = computed(() =>
  view.value.categories.map((name) => {
    const first = pieces.value.find((piece) => piece.category === name);
    return { id: name, label: t.value[`category.${name}`], medallion: (first && pictures[first.key]) || MEDALLIONS[name] || items };
  }),
);
const shownKit = computed(() => kit.value ?? view.value?.kits?.[0]?.key ?? null);
const shownPieces = computed(() =>
  pieces.value.filter(
    (piece) => piece.category === shownCategory.value && (shownCategory.value !== KITS || piece.kit === shownKit.value),
  ),
);
const picked = computed(() => pieces.value.find((piece) => piece.key === selected.value) ?? null);
const hasMark = computed(() => marked.value.width > 0 && marked.value.depth > 0);
const markTooBig = computed(() => Math.max(marked.value.width, marked.value.depth) > view.value.form.mostTiles);

const heading = computed(() => {
  if (view.value.plot) return fill(t.value.plot, { id: view.value.plot.id });
  return choosing.value ? t.value.choose_title : t.value.new_plot;
});

const choices = computed(() =>
  (view.value?.plots ?? []).map((plot) => ({
    id: plot.id,
    pieces: plot.pieces,
    name: fill(t.value.plot, { id: plot.id }),
    facts: fill(t.value.choice_facts, {
      kind: t.value[`kind.${plot.kind}`],
      width: plot.width,
      depth: plot.depth,
      pieces: plot.pieces,
      metres: plot.metres,
    }),
  })),
);

const facts = computed(() => {
  const plot = view.value.plot;
  if (!plot) return null;
  return fill(t.value.plot_facts, {
    kind: t.value[`kind.${plot.kind}`],
    width: plot.width,
    depth: plot.depth,
    height: plot.heightMetres,
    pieces: plot.pieces,
  });
});

const size = computed(() => {
  const metres = (tiles) => ((tiles * view.value.form.tileCentimetres) / CENTIMETRES_PER_METRE).toFixed(1);
  return fill(t.value.size, {
    width: marked.value.width,
    depth: marked.value.depth,
    metresWide: metres(marked.value.width),
    metresDeep: metres(marked.value.depth),
  });
});

// What the mouse carries first, then what the last change came to.
const status = computed(() => {
  if (fit.value !== NO_FIT) return { text: t.value[`fit.${fit.value}`], tone: fit.value === "Fits" ? "done" : "refused" };
  if (refused.value) return { text: t.value[`fit.${refused.value}`], tone: "refused" };
  if (answer.value) return { text: answer.value.text, tone: answer.value.done ? "done" : "refused" };
  if (choosing.value) return { text: t.value.choose_hint, tone: "" };
  return { text: marking.value ? t.value.mark_hint : t.value.build_hint, tone: "" };
});

function pointOf(event) {
  return [event.clientX / window.innerWidth, event.clientY / window.innerHeight];
}

function startDrag(piece, event) {
  drag = { piece };
  downAt = { x: event.clientX, y: event.clientY };
  carrying = false;
  refused.value = null;
  answer.value = null;
  lastPoint = pointOf(event);
  props.bridge.grab(...lastPoint, piece);
  window.addEventListener("pointermove", onPointerMove);
  window.addEventListener("pointerup", onPointerUp);
}

function onPieceDown(piece, event) {
  if (event.button !== 0) return;
  event.preventDefault();
  catalogue.value = false;
  startDrag(piece.key, event);
}

function pickStep(steps) {
  props.bridge.setStep(steps);
}

function pickSettle(value) {
  settle.value = value;
  props.bridge.setSettle(value);
}

function onWorldDown(event) {
  if (event.button === RIGHT_BUTTON) {
    startOrbit(event);
    return;
  }
  if (event.button !== 0) return;
  startDrag("", event);
}

function startOrbit(event) {
  event.preventDefault();
  orbitAt = { x: event.clientX, y: event.clientY };
  window.addEventListener("pointermove", onOrbitMove);
  window.addEventListener("pointerup", onOrbitUp);
}

// Sent at most once a frame, the pixels added up.
function onOrbitMove(event) {
  orbitBy.across += event.clientX - orbitAt.x;
  orbitBy.down += event.clientY - orbitAt.y;
  orbitAt = { x: event.clientX, y: event.clientY };
  if (orbitFrame) return;
  orbitFrame = requestAnimationFrame(() => {
    orbitFrame = 0;
    if (orbitBy.across || orbitBy.down) props.bridge.orbit(orbitBy.across, orbitBy.down);
    orbitBy = { across: 0, down: 0 };
  });
}

function onOrbitUp(event) {
  if (event.button === RIGHT_BUTTON) endOrbit();
}

function endOrbit() {
  orbitAt = null;
  window.removeEventListener("pointermove", onOrbitMove);
  window.removeEventListener("pointerup", onOrbitUp);
  if (orbitFrame) cancelAnimationFrame(orbitFrame);
  orbitFrame = 0;
  orbitBy = { across: 0, down: 0 };
}

function sendPan() {
  let forward = 0;
  let right = 0;
  for (const code of panning) {
    forward += PAN_KEYS[code][0];
    right += PAN_KEYS[code][1];
  }
  props.bridge.pan(forward, right);
}

function stopPan() {
  if (!panning.size) return;
  panning.clear();
  sendPan();
}

function onKeyup(event) {
  const code = readCode(event);
  if (code === "KeyZ") heightHeld = false;
  if (panning.delete(code)) sendPan();
}

function onBlur() {
  heightHeld = false;
  stopPan();
}

// At most once a frame, so a quick mouse does not flood the game.
function onPointerMove(event) {
  if (!carrying && Math.hypot(event.clientX - downAt.x, event.clientY - downAt.y) < DRAG_PIXELS) return;
  carrying = true;
  lastPoint = pointOf(event);
  if (frame) return;
  frame = requestAnimationFrame(() => {
    frame = 0;
    if (drag && lastPoint) props.bridge.point(...lastPoint);
  });
}

function onPointerUp(event) {
  endDrag();
  props.bridge.drop(...pointOf(event));
}

function endDrag() {
  drag = null;
  window.removeEventListener("pointermove", onPointerMove);
  window.removeEventListener("pointerup", onPointerUp);
  if (frame) cancelAnimationFrame(frame);
  frame = 0;
}

// Turns what the mouse carries, else brings the view nearer or further.
function onWheel(event) {
  event.preventDefault();
  if (!event.deltaY) return;
  if (building.value && drag && (drag.piece || carrying)) {
    const degrees = event.shiftKey ? FINE_TURN_DEGREES : turnStep.value;
    props.bridge.turn(event.deltaY > 0 ? degrees : -degrees);
    return;
  }
  props.bridge.zoom(event.deltaY > 0 ? 1 : -1);
}

function onContextMenu(event) {
  event.preventDefault();
}

function turn(sign, fine) {
  refused.value = null;
  props.bridge.turn(sign * (fine ? FINE_TURN_DEGREES : turnStep.value));
}

function raise(sign, fine) {
  refused.value = null;
  props.bridge.raise(sign * (fine ? FINE_RAISE_CENTIMETRES : heightStep.value));
}

function nudge(forward, right) {
  if (!selected.value) return;
  refused.value = null;
  props.bridge.nudge(forward, right);
}

function remove() {
  if (selected.value) props.bridge.remove();
}

function copy() {
  if (!picked.value) return;
  copied.value = picked.value.name;
  props.bridge.copy();
  answer.value = { text: fill(t.value.copied, { name: copied.value }), done: true };
}

function paste() {
  if (!copied.value) return;
  refused.value = null;
  props.bridge.paste(...(hoverAt ?? [MIDDLE, MIDDLE]));
}

function duplicate() {
  if (!selected.value) return;
  refused.value = null;
  props.bridge.duplicate();
}

function snapToFloor() {
  if (!selected.value) return;
  refused.value = null;
  props.bridge.snapToFloor();
}

// The plot's front and its sign to the next side: the one marked, or the one built on with nothing picked.
function turnFront() {
  if (building.value && selected.value) return;
  if (marking.value && !hasMark.value) return;
  refused.value = null;
  props.bridge.turnFront();
}

function breakApart() {
  if (!selected.value) return;
  refused.value = null;
  props.bridge.breakApart();
}

function onBreakRefused(why) {
  answer.value = { text: t.value[`break.${why}`], done: false };
}

function askRemove(plot) {
  removing.value = { id: plot.id, pieces: plot.pieces };
}

function removePlot() {
  if (!removing.value) return;
  answer.value = null;
  props.bridge.removePlot(removing.value.id);
  removing.value = null;
}

function choose(plot) {
  answer.value = null;
  props.bridge.choose(plot);
}

function newPlot() {
  answer.value = null;
  props.bridge.newPlot();
}

function onWorldHover(event) {
  hoverAt = pointOf(event);
}

function deselect() {
  props.bridge.deselect();
}

function readWhole(value, low, high) {
  const text = String(value).trim();
  if (!/^\d+$/.test(text)) return null;
  const number = Number(text);
  return number >= low && number <= high ? number : null;
}

function savePlot() {
  if (saving.value || !hasMark.value) return;
  const limits = view.value.form;
  if (markTooBig.value) {
    problem.value = { field: "size", text: fill(t.value.too_big, { most: limits.mostTiles }) };
    return;
  }
  const price = readWhole(form.price, 0, limits.mostGold);
  const weeklyTax = readWhole(form.weeklyTax, 0, limits.mostGold);
  const height = readWhole(form.height, 1, limits.mostHeightMetres);
  const goldProblem = fill(t.value.gold_invalid, { most: limits.mostGold });
  if (price === null) {
    problem.value = { field: "price", text: goldProblem };
    return;
  }
  if (weeklyTax === null) {
    problem.value = { field: "weeklyTax", text: goldProblem };
    return;
  }
  if (height === null) {
    problem.value = { field: "height", text: fill(t.value.height_invalid, { most: limits.mostHeightMetres }) };
    return;
  }

  problem.value = null;
  answer.value = null;
  saving.value = true;
  const json = JSON.stringify({ kind: form.kind, price, weeklyTax, heightMetres: height });
  props.bridge.savePlot(json);
}

function clearPlot() {
  problem.value = null;
  props.bridge.clearPlot();
}

function leave() {
  if (view.value) props.bridge.leave();
}

function toggleCatalogue() {
  if (drag) return;
  catalogue.value = !catalogue.value;
}

function onState(next) {
  const opening = !view.value || next.tool !== view.value.tool;
  view.value = next;
  saving.value = false;
  if (!opening) return;

  category.value = null;
  catalogue.value = next.tool === "Build";
  selected.value = "";
  marked.value = { width: 0, depth: 0 };
  problem.value = null;
  form.kind = next.form.kind;
  form.price = String(next.form.price);
  form.weeklyTax = String(next.form.weeklyTax);
  form.height = String(next.form.heightMetres);
}

function onAnswer(text, done) {
  saving.value = false;
  refused.value = null;
  answer.value = { text, done };
}

function onClosed() {
  endDrag();
  endOrbit();
  panning.clear();
  view.value = null;
  looking.value = false;
  answer.value = null;
  refused.value = null;
  fit.value = NO_FIT;
  selected.value = "";
}

function onLook(value) {
  endDrag();
  endOrbit();
  stopPan();
  looking.value = value;
}

// Esc lets go of one thing at a time: what the mouse carries, the catalogue, the piece picked, the plot marked, then the
// mode.
function onEscape() {
  if (removing.value) {
    removing.value = null;
  } else if (drag) {
    endDrag();
    props.bridge.cancel();
  } else if (catalogue.value && building.value) {
    catalogue.value = false;
  } else if (selected.value) {
    deselect();
  } else if (marking.value && hasMark.value) {
    clearPlot();
  } else {
    leave();
  }
}

const KEYS = {
  KeyQ: (event) => turn(-1, event.shiftKey),
  KeyE: (event) => turn(1, event.shiftKey),
  KeyF: () => snapToFloor(),
  KeyR: () => turnFront(),
  KeyX: () => deselect(),
  KeyG: () => props.bridge.step(),
  ArrowUp: (event) => (event.shiftKey ? raise(1, false) : nudge(1, 0)),
  ArrowDown: (event) => (event.shiftKey ? raise(-1, false) : nudge(-1, 0)),
  ArrowLeft: () => nudge(0, -1),
  ArrowRight: () => nudge(0, 1),
  KeyC: () => toggleCatalogue(),
};

// With Ctrl, while building.
const CONTROL_KEYS = {
  KeyC: () => copy(),
  KeyV: () => paste(),
  KeyD: () => duplicate(),
};

// A picked piece: a step a press, and with Z held W and S raise and lower it.
function moveByKey(code, event) {
  const [forward, right] = PAN_KEYS[code];
  if (heightHeld) {
    if (forward) raise(forward, event.shiftKey);
    return;
  }
  nudge(forward, right);
}

function onKeydown(event) {
  if (!view.value || looking.value || isComposing(event)) return;
  if (event.key === "Escape") {
    event.preventDefault();
    onEscape();
    return;
  }
  // A field of the plot's form keeps its letters and its Delete.
  if (event.target?.tagName === "INPUT") return;
  const code = readCode(event);
  if (code === "KeyZ") {
    event.preventDefault();
    heightHeld = true;
    return;
  }
  if (event.ctrlKey) {
    const act = building.value ? CONTROL_KEYS[code] : null;
    if (act) {
      event.preventDefault();
      act();
    }
    return;
  }
  if (PAN_KEYS[code]) {
    event.preventDefault();
    if (editing.value) {
      moveByKey(code, event);
      return;
    }
    if (!panning.has(code)) {
      panning.add(code);
      sendPan();
    }
    return;
  }
  if (isHeldPress(event) && event.key === "Delete") return;

  if (event.key === "Delete") {
    event.preventDefault();
    remove();
    return;
  }
  if (marking.value && code === "KeyR") {
    event.preventDefault();
    turnFront();
    return;
  }
  const act = building.value ? KEYS[code] : null;
  if (act) {
    event.preventDefault();
    act(event);
  }
}

watch(
  () => (view.value && catalogue.value && building.value ? shownPieces.value.map((piece) => piece.key) : []),
  (shown) => {
    if (shown.length) props.bridge.showing(shown);
  },
);

// The gizmo's height snaps to the step the panel picks.
watch(heightStep, (centimetres) => props.bridge.setHeight(centimetres));

onMounted(() => {
  props.bridge.onState(onState);
  props.bridge.onPieces((part, first) => (pieces.value = first ? part : [...pieces.value, ...part]));
  props.bridge.onAnswer(onAnswer);
  props.bridge.onClosed(onClosed);
  props.bridge.onLook(onLook);
  props.bridge.onFit((value) => (fit.value = value));
  props.bridge.onRefused((value) => (refused.value = value));
  props.bridge.onSelected((key) => {
    selected.value = key;
    // The keys that moved the view move the piece now
    if (key) stopPan();
  });
  props.bridge.onMarked((width, depth) => {
    marked.value = { width, depth };
    if (!width) saving.value = false;
  });
  props.bridge.onStep((value) => (step.value = value));
  props.bridge.onPicture((key, picture) => (pictures[key] = picture));
  props.bridge.onBreakRefused(onBreakRefused);
  window.addEventListener("keydown", onKeydown);
  window.addEventListener("keyup", onKeyup);
  window.addEventListener("blur", onBlur);
  props.bridge.signalReady();
  props.bridge.setHeight(heightStep.value);
});

onUnmounted(() => {
  window.removeEventListener("keydown", onKeydown);
  window.removeEventListener("keyup", onKeyup);
  window.removeEventListener("blur", onBlur);
  endDrag();
  endOrbit();
});
</script>

<template>
  <div v-if="view && looking" class="architect architect--looking">
    <p class="architect__looking" role="status">{{ t.looking }}</p>
  </div>

  <div v-else-if="view" class="architect" :data-tool="view.tool">
    <div
      class="architect__world"
      data-world
      @pointerdown="onWorldDown"
      @pointermove="onWorldHover"
      @wheel="onWheel"
      @contextmenu="onContextMenu"
    />

    <header class="architect__bar">
      <div class="architect__heading">
        <h1 class="architect__title">{{ t.title }}: {{ heading }}</h1>
        <p v-if="facts" class="architect__facts" data-facts>{{ facts }}</p>
      </div>
      <div class="architect__tools">
        <RpButton v-if="building" data-action="catalogue" :aria-expanded="catalogue" @click="toggleCatalogue">{{ t.pieces }}</RpButton>
        <RpButton v-if="choosing" data-action="new-plot" @click="newPlot">{{ t.new_plot_button }}</RpButton>
        <RpButton v-else data-action="plots" @click="choose(0)">{{ t.plots }}</RpButton>
        <RpButton data-action="leave" @click="leave">{{ t.close }}</RpButton>
      </div>
      <p class="architect__status" :class="status.tone && `architect__status--${status.tone}`" role="status" data-status>{{ status.text }}</p>
      <p class="architect__keys" data-keys>{{ editing ? t.edit_keys : t.view_keys }}</p>
      <p v-if="building && !editing" class="architect__keys">{{ t.keys }}</p>
    </header>

    <aside v-if="choosing" class="architect__panel architect__choices-panel" :aria-label="t.choose_title" data-choices>
      <p class="architect__setting-name">{{ t.plots_near }}</p>
      <ul v-if="choices.length" class="architect__plot-list">
        <li v-for="choice in choices" :key="choice.id" class="architect__plot-choice">
          <div>
            <p class="architect__plot-name">{{ choice.name }}</p>
            <p class="architect__plot-facts">{{ choice.facts }}</p>
          </div>
          <div class="architect__choice-acts">
            <RpButton :data-choose="choice.id" :aria-label="fill(t.build_on, { plot: choice.name })" @click="choose(choice.id)">{{ t.build }}</RpButton>
            <RpButton :data-remove="choice.id" :aria-label="fill(t.remove_named, { plot: choice.name })" @click="askRemove(choice)">
              {{ t.remove }}
            </RpButton>
          </div>
        </li>
      </ul>
      <p v-else class="architect__plot-facts" data-no-plots>{{ t.no_plots }}</p>
    </aside>

    <aside v-if="building" class="architect__panel" :aria-label="t.panel" data-panel>
        <div class="architect__setting" role="group" :aria-label="t.grid_label">
          <p class="architect__setting-name">{{ t.grid_label }}</p>
          <div class="architect__choices">
            <RpChip v-for="each in STEPS" :key="each" :pressed="step === each" :data-step="each" @click="pickStep(each)">
              {{ t[`grid_${each}`] }}
            </RpChip>
          </div>
        </div>
        <div class="architect__setting" role="group" :aria-label="t.turn_label">
          <p class="architect__setting-name">{{ t.turn_label }}</p>
          <div class="architect__choices">
            <RpChip v-for="each in TURNS" :key="each" :pressed="turnStep === each" :data-turn="each" @click="turnStep = each">
              {{ fill(t.degrees, { degrees: each }) }}
            </RpChip>
          </div>
        </div>
        <div class="architect__setting" role="group" :aria-label="t.height_label">
          <p class="architect__setting-name">{{ t.height_label }}</p>
          <div class="architect__choices">
            <RpChip v-for="each in HEIGHTS" :key="each" :pressed="heightStep === each" :data-height="each" @click="heightStep = each">
              {{ fill(t.centimetres, { centimetres: each }) }}
            </RpChip>
          </div>
        </div>
        <div class="architect__setting" role="group" :aria-label="t.plot_label">
          <p class="architect__setting-name">{{ t.plot_label }}</p>
          <div class="architect__choices">
            <RpButton data-action="turn-front" :disabled="!!selected" @click="turnFront">{{ t.turn_front }}</RpButton>
            <RpButton data-action="remove-plot" @click="askRemove(view.plot)">{{ t.remove_plot }}</RpButton>
          </div>
        </div>
        <div class="architect__setting" role="group" :aria-label="t.stand_label">
          <p class="architect__setting-name">{{ t.stand_label }}</p>
          <div class="architect__choices">
            <RpChip :pressed="settle" data-settle="under" @click="pickSettle(true)">{{ t.stand_under }}</RpChip>
            <RpChip :pressed="!settle" data-settle="ground" @click="pickSettle(false)">{{ t.stand_ground }}</RpChip>
          </div>
        </div>
    </aside>

    <section v-if="building && picked" class="architect__picked" :aria-label="fill(t.picked, { name: picked.name })" data-picked>
      <p class="architect__picked-name">{{ fill(t.picked, { name: picked.name }) }}</p>
      <div class="architect__arrows" role="group" :aria-label="t.move_label">
        <RpButton class="architect__arrow architect__arrow--up" :aria-label="t.move_forward" data-action="move-forward" @click="nudge(1, 0)">&#9650;</RpButton>
        <RpButton class="architect__arrow architect__arrow--left" :aria-label="t.move_left" data-action="move-left" @click="nudge(0, -1)">&#9664;</RpButton>
        <RpButton class="architect__arrow architect__arrow--right" :aria-label="t.move_right" data-action="move-right" @click="nudge(0, 1)">&#9654;</RpButton>
        <RpButton class="architect__arrow architect__arrow--down" :aria-label="t.move_back" data-action="move-back" @click="nudge(-1, 0)">&#9660;</RpButton>
      </div>
      <div class="architect__picked-acts">
        <RpButton data-action="turn-left" @click="turn(-1, false)">{{ t.turn_left }}</RpButton>
        <RpButton data-action="turn-right" @click="turn(1, false)">{{ t.turn_right }}</RpButton>
        <RpButton data-action="raise" @click="raise(1, false)">{{ t.raise }}</RpButton>
        <RpButton data-action="lower" @click="raise(-1, false)">{{ t.lower }}</RpButton>
        <RpButton data-action="snap" @click="snapToFloor">{{ t.snap_to_floor }}</RpButton>
        <RpButton data-action="duplicate" @click="duplicate">{{ t.duplicate }}</RpButton>
        <RpButton data-action="break-apart" @click="breakApart">{{ t.break_apart }}</RpButton>
        <RpButton data-action="remove" @click="remove">{{ t.remove }}</RpButton>
        <RpButton variant="primary" data-action="done" @click="deselect">{{ t.done }}</RpButton>
      </div>
    </section>

    <div v-if="building && catalogue" class="architect__catalogue" data-catalogue>
      <RpPanel class="architect__catalogue-panel" :title="t.drawer">
        <RpTabs v-model="shownCategory" :tabs="tabs" :label="t.drawer" :controls="panelId" />
        <div v-if="shownCategory === KITS" class="architect__kits" role="group" :aria-label="t.kit_label" data-kits>
          <RpChip v-for="each in view.kits" :key="each.key" :pressed="shownKit === each.key" :data-kit="each.key" @click="kit = each.key">
            {{ each.name }}
          </RpChip>
        </div>
        <ul :id="panelId" class="architect__pieces" role="tabpanel" :aria-labelledby="makeTabId(panelId, shownCategory)">
          <li v-for="piece in shownPieces" :key="piece.key">
            <button type="button" class="architect__piece" :data-piece="piece.key" @pointerdown="onPieceDown(piece, $event)">
              <img v-if="pictures[piece.key]" class="architect__picture" :src="pictures[piece.key]" alt="">
              <span v-else class="architect__picture architect__picture--waiting" />
              <span class="architect__piece-name">{{ piece.name }}</span>
              <span class="architect__piece-use">{{ t[`use.${piece.use}`] }}</span>
            </button>
          </li>
        </ul>
        <div class="architect__catalogue-foot">
          <p class="architect__catalogue-hint">{{ t.catalogue_hint }}</p>
          <RpButton data-action="close-catalogue" @click="toggleCatalogue">{{ t.close_catalogue }}</RpButton>
        </div>
      </RpPanel>
    </div>

    <section v-if="removing" class="architect__confirm" role="alertdialog" :aria-labelledby="removeId" data-confirm>
      <p :id="removeId" class="architect__question">{{ fill(t.remove_confirm, { id: removing.id, pieces: removing.pieces }) }}</p>
      <div class="architect__confirm-acts">
        <RpButton data-action="keep-plot" @click="removing = null">{{ t.remove_no }}</RpButton>
        <RpButton variant="primary" data-action="confirm-remove" @click="removePlot">{{ t.remove_yes }}</RpButton>
      </div>
    </section>

    <RpPanel v-if="marking && hasMark" class="architect__form" :title="t.form" data-form>
      <p class="architect__size" data-size>{{ size }}</p>
      <div class="architect__front">
        <RpButton data-action="turn-front" @click="turnFront">{{ t.turn_front }}</RpButton>
      </div>
      <form novalidate @submit.prevent="savePlot">
        <fieldset class="architect__kinds">
          <legend class="architect__label">{{ t.kind }}</legend>
          <RpChip
            v-for="kind in ['Residential', 'Industrial']"
            :key="kind"
            :pressed="form.kind === kind"
            :data-kind="kind"
            @click="form.kind = kind"
          >
            {{ t[`kind.${kind}`] }}
          </RpChip>
        </fieldset>
        <RpField v-model="form.price" :label="t.price" inputmode="numeric" data-field="price" :invalid="problem?.field === 'price'" />
        <RpField v-model="form.weeklyTax" :label="t.weekly_tax" inputmode="numeric" data-field="weekly-tax" :invalid="problem?.field === 'weeklyTax'" />
        <RpField v-model="form.height" :label="t.height" inputmode="numeric" data-field="height" :invalid="problem?.field === 'height'" />
        <p v-if="problem" class="architect__problem" role="alert">{{ problem.text }}</p>
        <div class="architect__form-acts">
          <RpButton data-action="clear" @click="clearPlot">{{ t.clear }}</RpButton>
          <RpButton variant="primary" type="submit" :disabled="saving" :aria-busy="saving" data-action="save">
            {{ saving ? t.saving : t.save }}
          </RpButton>
        </div>
      </form>
    </RpPanel>
  </div>
</template>
