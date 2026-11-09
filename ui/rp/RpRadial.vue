<script setup>
import { computed, useId, useTemplateRef } from "vue";
import RpIcon from "./RpIcon.vue";

// items: [{ id, label, icon, disabled, reason, key }], key the one a wedge shows, its place from 1 without one. The
// highlight is the v-model: a disabled wedge lights up too, so the hub can say why it is closed, but it is never
// selected. turn rotates the wedges clockwise by that share of one. caption is a line under the title.
const props = defineProps({
  items: { type: Array, default: () => [] },
  title: { type: String, default: "" },
  caption: { type: String, default: "" },
  label: { type: String, required: true },
  turn: { type: Number, default: 0 },
});
const highlight = defineModel({ type: String, default: null });
const emit = defineEmits(["select"]);

// The size the wheel is drawn at. The CSS scales the whole drawing.
const DRAWN_SIZE = 380;
const RING_INSET = 12;
const INNER_RATIO = 0.28;
const SEGMENT_GAP_DEGREES = 1.2;
const RING_OFFSET = 6;
const ARC_OFFSET = 2;
const FULL_CIRCLE = 360;
const HALF_CIRCLE = 180;
// Angles count from three o'clock, the first wedge sits at twelve.
const TOP = -90;
const PERCENT = 100;
// The pointer has to leave this share of the inner radius before it picks a wedge.
const DEAD_ZONE = 0.6;
// Where a wedge's icon and number sit, as a share of the way from the inner ring to the rim: the number by the hub,
// clear of the icon.
const ICON_SHARE = 0.56;
const NUMBER_SHARE = 0.14;
const KEY_STEP = { ArrowRight: 1, ArrowDown: 1, ArrowLeft: -1, ArrowUp: -1 };

const root = useTemplateRef("root");
const optionId = useId();

const geometry = computed(() => {
  const center = DRAWN_SIZE / 2;
  const count = Math.max(props.items.length, 1);
  return {
    center,
    outer: center - RING_INSET,
    inner: Math.round(DRAWN_SIZE * INNER_RATIO),
    count,
    segment: FULL_CIRCLE / count,
    gap: count > 1 ? SEGMENT_GAP_DEGREES : 0,
  };
});
const currentIndex = computed(() => props.items.findIndex((item) => item.id === highlight.value));
const current = computed(() => props.items[currentIndex.value]);

function plotPoint(center, radius, degrees) {
  const angle = (degrees * Math.PI) / HALF_CIRCLE;
  return [center + radius * Math.cos(angle), center + radius * Math.sin(angle)];
}

function drawSector(center, outer, inner, startDegrees, endDegrees) {
  const large = endDegrees - startDegrees > HALF_CIRCLE ? 1 : 0;
  const [ox1, oy1] = plotPoint(center, outer, startDegrees);
  const [ox2, oy2] = plotPoint(center, outer, endDegrees);
  const [ix2, iy2] = plotPoint(center, inner, endDegrees);
  const [ix1, iy1] = plotPoint(center, inner, startDegrees);
  return `M${ox1} ${oy1}A${outer} ${outer} 0 ${large} 1 ${ox2} ${oy2}L${ix2} ${iy2}A${inner} ${inner} 0 ${large} 0 ${ix1} ${iy1}Z`;
}

const middleOf = (index) => TOP + (index + props.turn) * geometry.value.segment;

function drawWedge(index) {
  const { center, outer, inner, segment, gap } = geometry.value;
  const middle = middleOf(index);
  return drawSector(center, outer, inner, middle - segment / 2 + gap, middle + segment / 2 - gap);
}

const rimArc = computed(() => {
  const { center, outer, segment, gap } = geometry.value;
  const middle = middleOf(currentIndex.value);
  const radius = outer + ARC_OFFSET;
  const [x1, y1] = plotPoint(center, radius, middle - segment / 2 + gap);
  const [x2, y2] = plotPoint(center, radius, middle + segment / 2 - gap);
  return `M${x1} ${y1}A${radius} ${radius} 0 0 1 ${x2} ${y2}`;
});

function place(index, share) {
  const { center, outer, inner } = geometry.value;
  const [x, y] = plotPoint(center, inner + (outer - inner) * share, middleOf(index));
  return { left: `${(x / DRAWN_SIZE) * PERCENT}%`, top: `${(y / DRAWN_SIZE) * PERCENT}%` };
}

const placeIcon = (index) => place(index, ICON_SHARE);
const placeNumber = (index) => place(index, NUMBER_SHARE);

function followPointer(event) {
  const { inner, count, segment } = geometry.value;
  const box = root.value.getBoundingClientRect();
  const dx = event.clientX - box.left - box.width / 2;
  const dy = event.clientY - box.top - box.height / 2;
  if (Math.hypot(dx, dy) < inner * (box.width / DRAWN_SIZE) * DEAD_ZONE) {
    highlight.value = null;
    return;
  }
  const degrees = (Math.atan2(dy, dx) * HALF_CIRCLE) / Math.PI - TOP - props.turn * segment;
  const index = Math.round((((degrees % FULL_CIRCLE) + FULL_CIRCLE) % FULL_CIRCLE) / segment) % count;
  highlight.value = props.items[index]?.id ?? null;
}

function confirm() {
  if (current.value && !current.value.disabled) emit("select", current.value.id);
}

function onKeydown(event) {
  const step = KEY_STEP[event.key];
  if (step && props.items.length) {
    event.preventDefault();
    const from = currentIndex.value < 0 ? (step > 0 ? -1 : 0) : currentIndex.value;
    highlight.value = props.items[(from + step + props.items.length) % props.items.length].id;
  } else if (event.key === "Enter" || event.key === " ") {
    event.preventDefault();
    confirm();
  }
}

defineExpose({ focus: () => root.value.focus() });
</script>

<template>
  <div
    ref="root"
    class="rp-radial"
    role="listbox"
    tabindex="0"
    :aria-label="label"
    :aria-activedescendant="current ? `${optionId}-${currentIndex}` : undefined"
    @mousemove="followPointer"
    @click="confirm"
    @keydown="onKeydown"
  >
    <svg class="rp-radial__wheel" :viewBox="`0 0 ${DRAWN_SIZE} ${DRAWN_SIZE}`" aria-hidden="true">
      <path
        v-for="(item, index) in items"
        :key="item.id"
        :d="drawWedge(index)"
        class="rp-radial__wedge"
        :class="{ 'rp-radial__wedge--on': index === currentIndex, 'rp-radial__wedge--disabled': item.disabled }"
      />
      <circle :cx="geometry.center" :cy="geometry.center" :r="geometry.outer + RING_OFFSET" class="rp-radial__ring" />
      <circle :cx="geometry.center" :cy="geometry.center" :r="geometry.inner - RING_OFFSET" class="rp-radial__hub-ring" />
      <path v-if="currentIndex >= 0" class="rp-radial__arc" :d="rimArc" />
    </svg>
    <span
      v-for="(item, index) in items"
      :id="`${optionId}-${index}`"
      :key="item.id"
      role="option"
      class="rp-radial__item"
      :class="{ 'rp-radial__item--on': index === currentIndex, 'rp-radial__item--disabled': item.disabled }"
      :aria-selected="index === currentIndex ? 'true' : 'false'"
      :aria-disabled="item.disabled ? 'true' : undefined"
      :aria-label="item.label"
      :style="placeIcon(index)"
    ><RpIcon :name="item.icon" /></span>
    <span v-for="(item, index) in items" :key="`${item.id}-number`" class="rp-radial__number" :style="placeNumber(index)" aria-hidden="true">
      {{ item.key ?? index + 1 }}
    </span>
    <div class="rp-radial__hub" aria-live="polite">
      <span v-if="title" class="rp-radial__title">{{ title }}</span>
      <span v-if="caption" class="rp-radial__caption">{{ caption }}</span>
      <span v-if="current" class="rp-radial__label">{{ current.label }}</span>
      <span v-if="current?.disabled" class="rp-radial__reason">{{ current.reason }}</span>
    </div>
  </div>
</template>
