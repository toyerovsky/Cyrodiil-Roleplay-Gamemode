<script setup>
import { computed, onMounted, onUnmounted, ref, useTemplateRef, watch } from "vue";
import { createBoard } from "../dice/board/scene.js";
import { boardForSeat, createSeatPlanner, seatEvents } from "./table.js";

const props = defineProps({
  match: { type: Object, required: true }, seat: { type: Number, required: true },
  label: { type: String, required: true }, picked: { type: Number, required: true },
  enabled: Boolean, takeAnimation: { type: Function, required: true }, sound: { type: Object, required: true },
  receivedAt: { type: Number, required: true },
});
const emit = defineEmits(["pick"]);
const canvas = useTemplateRef("canvas");
const tags = useTemplateRef("tags");
const host = useTemplateRef("host");
const phase = ref("loading");
const events = computed(() => seatEvents(props.match, props.seat));
const plan = createSeatPlanner();
let board;
let resize;
let stopped = false;
let ready = false;
let revision = 0;

async function display() {
  if (!ready || stopped) return;
  const request = ++revision;
  const last = plan(events.value);
  if (!last) { phase.value = "ready"; return; }
  const animate = props.takeAnimation(last.id) && last.id === props.match.activeThrow;
  phase.value = "loading";
  const elapsed = props.match.throwElapsedMs + Date.now() - props.receivedAt;
  await board.playShared(last.values, last.set, last.data, elapsed, props.match.animationMs, animate);
  if (stopped || request !== revision) return;
  if (phase.value !== "rest") phase.value = animate ? "flight" : "rest";
  board.select(props.picked);
}

watch(() => events.value.at(-1)?.id, display);
watch(() => props.picked, (mask) => board?.select(mask));

function pick(event) {
  props.sound.wake();
  if (!props.enabled) return;
  const index = board?.pick(event.clientX, event.clientY) ?? -1;
  if (index >= 0) emit("pick", index);
}

onMounted(async () => {
  board = createBoard({ canvas: canvas.value, tags: tags.value, sound: props.sound,
    stage: () => null, boardModel: boardForSeat(props.seat), fit: "board", onLanded: () => { phase.value = "rest"; },
    onTimeout: () => { throw new Error("A shared throw already has its server values."); } });
  resize = new ResizeObserver(() => board.resize());
  resize.observe(host.value);
  await board.start();
  ready = true;
  if (!stopped) await display();
});
onUnmounted(() => { stopped = true; revision++; resize?.disconnect(); board?.dispose(); });
</script>

<template>
  <div ref="host" class="gambling__board" :data-seat="seat" :data-board="boardForSeat(seat)" :data-phase="phase" :data-picked="picked">
    <canvas ref="canvas" :aria-label="label" role="img" @click="pick" />
    <div ref="tags" class="gambling__tags" aria-hidden="true" />
  </div>
</template>
