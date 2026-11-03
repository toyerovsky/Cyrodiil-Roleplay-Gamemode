<script setup>
import { computed, nextTick, onMounted, onUnmounted, ref, useId, useTemplateRef } from "vue";
import { RpButton, RpChip, RpIcon, RpPanel } from "../rp/index.js";
import { fill, pickTexts, usePageTexts } from "../shared/locale.js";
import { isComposing, isHeldPress } from "../shared/press.js";
import { WARE_COLUMNS, WARE_ICONS } from "./wares.js";

const props = defineProps({ bridge: { type: Object, required: true } });

const ALL = "";

const { locale, t } = usePageTexts(props.bridge, "trader");
const names = computed(() => pickTexts(locale.value, "ui.wares."));

const shop = ref(null);
const category = ref(ALL);
const selectedId = ref("");
const count = ref(1);
const answer = ref(null);
const busy = ref(false);
const list = useTemplateRef("list");
const listId = useId();
const statusId = useId();

let asked = null;

const categories = computed(() => [...new Set(shop.value?.wares.map((ware) => ware.category) ?? [])]);
const shown = computed(() => shop.value?.wares.filter((ware) => category.value === ALL || ware.category === category.value) ?? []);
const selected = computed(() => shown.value.find((ware) => ware.id === selectedId.value) ?? null);
const total = computed(() => (selected.value ? selected.value.price * count.value : 0));
const affordable = computed(() => (selected.value ? Math.floor(shop.value.gold / selected.value.price) : 0));
const most = computed(() => Math.max(1, Math.min(shop.value?.most ?? 1, affordable.value)));
const canBuy = computed(() => !!selected.value && !busy.value && total.value <= shop.value.gold);

const findName = (ware) => names.value[ware.id] ?? ware.id;
const placeIcon = (ware) => {
  const place = WARE_ICONS[ware.id] ?? 0;
  return { "--columns": WARE_COLUMNS, "--col": place % WARE_COLUMNS, "--row": Math.floor(place / WARE_COLUMNS) };
};
const formatWeight = (ware) => Number(ware.weight.toFixed(1)).toLocaleString(locale.value);

async function onState(view) {
  const opening = !shop.value;
  shop.value = view;
  if (!opening) {
    count.value = Math.min(count.value, most.value);
    return;
  }

  category.value = ALL;
  answer.value = null;
  busy.value = false;
  select(view.wares[0]?.id ?? "");
  await nextTick();
  list.value?.focus();
}

function select(id) {
  selectedId.value = id;
  count.value = 1;
}

function pickCategory(key) {
  category.value = key;
  if (!selected.value) select(shown.value[0]?.id ?? "");
}

function changeCount(by) {
  count.value = Math.min(most.value, Math.max(1, count.value + by));
}

function buy() {
  if (!canBuy.value) return;
  busy.value = true;
  answer.value = null;
  asked = { name: findName(selected.value), count: count.value };
  props.bridge.buy(selected.value.id, count.value);
}

function onAnswer(text, bought) {
  busy.value = false;
  answer.value = { text: bought && asked ? `${fill(t.value.bought, asked)} ${text}` : text, bought };
}

function close() {
  shop.value = null;
  answer.value = null;
  busy.value = false;
}

function leave() {
  if (shop.value) props.bridge.close();
}

function onKeydown(event) {
  if (!shop.value || isComposing(event)) return;
  if (isHeldPress(event)) {
    event.preventDefault();
    return;
  }

  const index = shown.value.findIndex((ware) => ware.id === selectedId.value);
  const moves = { ArrowDown: index + 1, ArrowUp: index - 1, Home: 0, End: shown.value.length - 1 };
  if (event.key in moves && shown.value.length > 0) {
    event.preventDefault();
    const next = shown.value[Math.min(shown.value.length - 1, Math.max(0, moves[event.key]))];
    select(next.id);
    nextTick(() => list.value?.querySelector(`[data-ware="${next.id}"]`)?.scrollIntoView({ block: "nearest" }));
  } else if (event.key === "+" || event.key === "ArrowRight") {
    event.preventDefault();
    changeCount(1);
  } else if (event.key === "-" || event.key === "ArrowLeft") {
    event.preventDefault();
    changeCount(-1);
  } else if (event.key === "Enter" && event.target === list.value) {
    event.preventDefault();
    buy();
  } else if (event.key === "Escape") {
    event.preventDefault();
    leave();
  }
}

onMounted(() => {
  props.bridge.onState(onState);
  props.bridge.onAnswer(onAnswer);
  props.bridge.onClosed(close);
  window.addEventListener("keydown", onKeydown);
  props.bridge.signalReady();
});

onUnmounted(() => window.removeEventListener("keydown", onKeydown));
</script>

<template>
  <div v-if="shop" class="trader">
    <RpPanel class="trader__panel" variant="letter" :title="t[`shop.${shop.shop}`] ?? shop.shop">
      <p class="trader__purse">
        <RpIcon name="coins" class="trader__coin" />
        <span>{{ t.purse }}</span>
        <strong>{{ fill(t.gold, { count: shop.gold }) }}</strong>
      </p>

      <nav class="trader__categories" :aria-label="t.categories">
        <RpChip v-for="key in [ALL, ...categories]" :key="key" :pressed="category === key" @click="pickCategory(key)">
          {{ key === ALL ? t.all : t[`category.${key}`] }}
        </RpChip>
      </nav>

      <div class="trader__body">
        <ul
          :id="listId"
          ref="list"
          class="trader__wares"
          role="listbox"
          tabindex="0"
          :aria-label="t.wares"
          :aria-activedescendant="selected ? `${listId}-${selected.id}` : undefined"
          :aria-describedby="statusId"
        >
          <li
            v-for="ware in shown"
            :id="`${listId}-${ware.id}`"
            :key="ware.id"
            :data-ware="ware.id"
            role="option"
            class="trader__ware"
            :class="{ 'trader__ware--on': ware.id === selectedId, 'trader__ware--dear': ware.price > shop.gold }"
            :aria-selected="ware.id === selectedId"
            @click="select(ware.id)"
            @dblclick="buy"
          >
            <span class="trader__icon" :style="placeIcon(ware)" />
            <span class="trader__name">{{ findName(ware) }}</span>
            <span class="trader__price">{{ fill(t.gold, { count: ware.price }) }}</span>
          </li>
        </ul>

        <section v-if="selected" class="trader__detail" :aria-label="findName(selected)">
          <span class="trader__icon trader__icon--large" :style="placeIcon(selected)" />
          <h2 class="trader__detail-name">{{ findName(selected) }}</h2>
          <p class="trader__facts">
            {{ t[`category.${selected.category}`] }} · {{ fill(t.weight, { weight: formatWeight(selected) }) }}
          </p>
          <p class="trader__each">{{ fill(t.each, { count: selected.price }) }}</p>

          <div class="trader__count" role="group" :aria-label="t.count">
            <RpButton class="trader__step" :aria-label="t.fewer" :disabled="count <= 1" @click="changeCount(-1)">−</RpButton>
            <output class="trader__amount" aria-live="polite">{{ count }}</output>
            <RpButton class="trader__step" :aria-label="t.more" :disabled="count >= most" @click="changeCount(1)">+</RpButton>
          </div>

          <p class="trader__total" :class="{ 'trader__total--dear': total > shop.gold }">{{ fill(t.total, { count: total }) }}</p>
          <RpButton class="trader__buy" variant="primary" :disabled="!canBuy" :aria-busy="busy" @click="buy">
            {{ total > shop.gold ? t.too_poor : t.buy }}
          </RpButton>
        </section>
      </div>

      <p :id="statusId" class="trader__status" :class="answer && (answer.bought ? 'trader__status--bought' : 'trader__status--refused')" role="status">
        {{ answer?.text ?? t.hint }}
      </p>

      <div class="trader__footer">
        <RpButton @click="leave">{{ t.close }}</RpButton>
      </div>
    </RpPanel>
  </div>
</template>
