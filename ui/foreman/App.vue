<script setup>
import { computed, nextTick, onMounted, onUnmounted, ref, useId, useTemplateRef } from "vue";
import { RpButton, RpChip, RpIcon, RpPanel } from "../rp/index.js";
import { fill, usePageTexts } from "../shared/locale.js";
import { isComposing, isHeldPress } from "../shared/press.js";
import { WARE_COLUMNS, WARE_ICONS } from "../trader/wares.js";

const props = defineProps({ bridge: { type: Object, required: true } });

const BUY = "buy";
const SELL = "sell";
const PAY = "pay";

const { t } = usePageTexts(props.bridge, "foreman");

const foreman = ref(null);
const kind = ref(BUY);
const selectedId = ref("");
const count = ref(1);
const answer = ref(null);
const busy = ref(false);
const list = useTemplateRef("list");
const listId = useId();
const statusId = useId();

const kinds = computed(() => [BUY, SELL].filter((key) => foreman.value?.offers.some((offer) => offer.kind === key)));
const shown = computed(() => foreman.value?.offers.filter((offer) => offer.kind === kind.value) ?? []);
const selected = computed(() => shown.value.find((offer) => offer.id === selectedId.value) ?? null);
const total = computed(() => (selected.value ? selected.value.price * count.value : 0));
// The most pieces a deal can take now: what the gold buys, or what the player carries to sell
const most = computed(() => {
  if (!selected.value) return 0;
  const room = selected.value.kind === BUY ? Math.floor(foreman.value.gold / selected.value.price) : selected.value.carried;
  return Math.min(foreman.value.most, room);
});
const canDeal = computed(() => !!selected.value && !busy.value && most.value >= 1);
const owed = computed(() => (foreman.value ? foreman.value.unpaid * foreman.value.wage : 0));

const placeIcon = (offer) => {
  const place = WARE_ICONS[offer.icon];
  return { "--columns": WARE_COLUMNS, "--col": place % WARE_COLUMNS, "--row": Math.floor(place / WARE_COLUMNS) };
};
const hasIcon = (offer) => offer.icon in WARE_ICONS;
const dealLabel = computed(() => {
  if (!selected.value) return "";
  if (most.value >= 1) return selected.value.kind === BUY ? t.value.buy : t.value.sell;
  return selected.value.kind === BUY ? t.value.too_poor : t.value.none_carried;
});

async function onState(view) {
  const opening = !foreman.value;
  foreman.value = view;
  if (!opening) {
    if (!selected.value) select(shown.value[0]?.id ?? "");
    count.value = Math.max(1, Math.min(count.value, most.value));
    return;
  }

  kind.value = kinds.value[0] ?? BUY;
  answer.value = null;
  busy.value = false;
  select(shown.value[0]?.id ?? "");
  await nextTick();
  list.value?.focus();
}

function select(id) {
  selectedId.value = id;
  count.value = 1;
}

function pickKind(key) {
  kind.value = key;
  select(shown.value[0]?.id ?? "");
}

function changeCount(by) {
  count.value = Math.max(1, Math.min(Math.max(1, most.value), count.value + by));
}

function deal() {
  if (!canDeal.value) return;
  busy.value = true;
  answer.value = null;
  props.bridge.deal(selected.value.id, count.value);
}

function collect() {
  if (busy.value || !foreman.value?.unpaid) return;
  busy.value = true;
  answer.value = null;
  props.bridge.deal(PAY, 1);
}

function onAnswer(text, done) {
  busy.value = false;
  answer.value = { text, done };
}

function close() {
  foreman.value = null;
  answer.value = null;
  busy.value = false;
}

function leave() {
  if (foreman.value) props.bridge.close();
}

function onKeydown(event) {
  if (!foreman.value || isComposing(event)) return;
  if (isHeldPress(event)) {
    event.preventDefault();
    return;
  }

  const index = shown.value.findIndex((offer) => offer.id === selectedId.value);
  const moves = { ArrowDown: index + 1, ArrowUp: index - 1, Home: 0, End: shown.value.length - 1 };
  if (event.key in moves && shown.value.length > 0) {
    event.preventDefault();
    const next = shown.value[Math.min(shown.value.length - 1, Math.max(0, moves[event.key]))];
    select(next.id);
    nextTick(() => list.value?.querySelector(`[data-offer="${CSS.escape(next.id)}"]`)?.scrollIntoView({ block: "nearest" }));
  } else if (event.key === "+" || event.key === "ArrowRight") {
    event.preventDefault();
    changeCount(1);
  } else if (event.key === "-" || event.key === "ArrowLeft") {
    event.preventDefault();
    changeCount(-1);
  } else if (event.key === "Enter" && event.target === list.value) {
    event.preventDefault();
    deal();
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
  <div v-if="foreman" class="trader foreman">
    <RpPanel class="trader__panel foreman__panel" :class="{ 'foreman__panel--wages': kinds.length === 0 }" variant="letter" :title="foreman.name">
      <p class="trader__purse">
        <RpIcon name="coins" class="trader__coin" />
        <span>{{ t.purse }}</span>
        <strong>{{ fill(t.gold, { count: foreman.gold }) }}</strong>
      </p>

      <section v-if="foreman.paysWages" class="foreman__wages" :aria-label="t.wages">
        <div class="foreman__owed">
          <h2 class="foreman__wages-title">{{ t.wages }}</h2>
          <p class="foreman__wages-line">{{ fill(t.delivered, { count: foreman.unpaid }) }}</p>
          <p class="foreman__wages-sum">{{ fill(t.owed, { count: owed }) }}</p>
        </div>
        <RpButton class="foreman__collect" variant="primary" :disabled="busy || foreman.unpaid === 0" :aria-busy="busy" @click="collect">
          {{ foreman.unpaid === 0 ? t.nothing_owed : t.collect }}
        </RpButton>
      </section>

      <nav v-if="kinds.length > 1" class="trader__categories" :aria-label="t.kinds">
        <RpChip v-for="key in kinds" :key="key" :pressed="kind === key" @click="pickKind(key)">
          {{ key === BUY ? t.for_sale : t.wanted }}
        </RpChip>
      </nav>

      <div v-if="kinds.length > 0" class="trader__body">
        <ul
          :id="listId"
          ref="list"
          class="trader__wares"
          role="listbox"
          tabindex="0"
          :aria-label="kind === BUY ? t.for_sale : t.wanted"
          :aria-activedescendant="selected ? `${listId}-${selected.id}` : undefined"
          :aria-describedby="statusId"
        >
          <li
            v-for="offer in shown"
            :id="`${listId}-${offer.id}`"
            :key="offer.id"
            :data-offer="offer.id"
            role="option"
            class="trader__ware"
            :class="{
              'trader__ware--on': offer.id === selectedId,
              'trader__ware--dear': offer.kind === BUY ? offer.price > foreman.gold : offer.carried === 0,
            }"
            :aria-selected="offer.id === selectedId"
            @click="select(offer.id)"
            @dblclick="deal"
          >
            <span v-if="hasIcon(offer)" class="trader__icon" :style="placeIcon(offer)" />
            <RpIcon v-else name="hand" class="foreman__no-icon" />
            <span class="trader__name">{{ offer.name }}</span>
            <span class="trader__price">
              {{ offer.kind === SELL ? fill(t.carried_price, { carried: offer.carried, count: offer.price }) : fill(t.gold, { count: offer.price }) }}
            </span>
          </li>
        </ul>

        <section v-if="selected" class="trader__detail" :aria-label="selected.name">
          <span v-if="hasIcon(selected)" class="trader__icon trader__icon--large" :style="placeIcon(selected)" />
          <RpIcon v-else name="hand" class="foreman__no-icon foreman__no-icon--large" />
          <h2 class="trader__detail-name">{{ selected.name }}</h2>
          <p class="trader__each">{{ fill(selected.kind === BUY ? t.each : t.paid_each, { count: selected.price }) }}</p>
          <p v-if="selected.kind === SELL" class="trader__facts">{{ fill(t.carried, { count: selected.carried }) }}</p>

          <div class="trader__count" role="group" :aria-label="t.count">
            <RpButton class="trader__step" :aria-label="t.fewer" :disabled="count <= 1" @click="changeCount(-1)">−</RpButton>
            <output class="trader__amount" aria-live="polite">{{ count }}</output>
            <RpButton class="trader__step" :aria-label="t.more" :disabled="count >= most" @click="changeCount(1)">+</RpButton>
          </div>

          <p class="trader__total" :class="{ 'trader__total--dear': most < 1 }">
            {{ fill(selected.kind === BUY ? t.total_pay : t.total_get, { count: total }) }}
          </p>
          <RpButton class="trader__buy" variant="primary" :disabled="!canDeal" :aria-busy="busy" @click="deal">{{ dealLabel }}</RpButton>
        </section>
      </div>

      <p :id="statusId" class="trader__status" :class="answer && (answer.done ? 'trader__status--bought' : 'trader__status--refused')" role="status">
        {{ answer?.text ?? (kinds.length > 0 ? t.hint : t.hint_wages) }}
      </p>

      <div class="trader__footer">
        <RpButton @click="leave">{{ t.close }}</RpButton>
      </div>
    </RpPanel>
  </div>
</template>
