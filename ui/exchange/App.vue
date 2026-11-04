<script setup>
import { computed, nextTick, onMounted, onUnmounted, ref, shallowRef, useId, useTemplateRef } from "vue";
import { RpButton, RpField, RpIcon, RpPanel } from "../rp/index.js";
import { fill, usePageTexts } from "../shared/locale.js";
import { isComposing, isHeldPress } from "../shared/press.js";
import CarriedItems from "./CarriedItems.vue";

const props = defineProps({ bridge: { type: Object, required: true } });

// Typing pauses this long before the offer goes, so each digit is not an offer that takes the confirmations back.
const OFFER_AFTER_MS = 400;
const TICK_MS = 250;
const MS_PER_SECOND = 1000;
const DIGITS = /^\d*$/;
// Within what the server's whole numbers hold.
const MAX_DIGITS = 9;

const { t } = usePageTexts(props.bridge, "exchange");

const deal = ref(null);
const amount = ref("");
// The items of the offer as the player sees them, each with the `text` of its count, ahead of the server while a change
// waits.
const rows = ref([]);
const refusal = ref("");
const done = ref("");
const now = ref(Date.now());
const field = useTemplateRef("field");
const carriedId = useId();

// The offer the player is still typing waits on this timer, 0 while none waits.
const offerTimer = shallowRef(0);

let confirmAt = 0;
let ticker = 0;

const secondsLeft = computed(() => Math.max(0, Math.ceil((confirmAt - now.value) / MS_PER_SECOND)));
const canConfirm = computed(() => deal.value && !done.value && !deal.value.mine.confirmed && secondsLeft.value === 0 && !offerTimer.value);
const available = computed(() => deal.value?.items.map((item) => ({ ...item, left: Math.max(0, item.count - countOffered(item.key)) })) ?? []);
const confirmLabel = computed(() => {
  if (done.value) return t.value.done;
  if (deal.value?.mine.confirmed) return fill(t.value.waiting, { name: deal.value.other });
  if (secondsLeft.value > 0) return fill(t.value.confirm_in, { seconds: secondsLeft.value });
  return t.value.confirm;
});

const toRow = (item, count) => ({ key: item.key, name: item.name, used: item.used, text: String(count) });
const readCount = (row) => (DIGITS.test(row.text.trim()) ? Number(row.text.trim()) : 0);
const countOffered = (key) => {
  const row = rows.value.find((each) => each.key === key);
  return row ? readCount(row) : 0;
};
const countCarried = (key) => deal.value.items.find((item) => item.key === key)?.count ?? 0;
const describe = (item) => (item.used ? fill(t.value.used, { name: item.name }) : item.name);

async function onState(view) {
  const opening = !deal.value;
  deal.value = view;
  confirmAt = Date.now() + view.confirmInMs;
  now.value = Date.now();
  // What the player is typing stays theirs until it goes.
  if (!offerTimer.value) {
    amount.value = String(view.mine.gold);
    rows.value = view.mine.items.map((item) => toRow(item, item.count));
  }
  if (!opening) return;

  done.value = "";
  refusal.value = "";
  ticker = setInterval(() => (now.value = Date.now()), TICK_MS);
  await nextTick();
  field.value.focus();
  field.value.select();
}

function close() {
  deal.value = null;
  rows.value = [];
  done.value = "";
  refusal.value = "";
  clearTimeout(offerTimer.value);
  offerTimer.value = 0;
  clearInterval(ticker);
}

function onInput() {
  refusal.value = "";
  clearTimeout(offerTimer.value);
  offerTimer.value = setTimeout(sendOffer, OFFER_AFTER_MS);
}

function addPiece(item) {
  const row = rows.value.find((each) => each.key === item.key);
  if (row) row.text = String(readCount(row) + 1);
  else rows.value.push(toRow(item, 1));
  onInput();
}

function changePieces(row, by) {
  const count = readCount(row) + by;
  if (count > 0) row.text = String(count);
  else rows.value = rows.value.filter((each) => each !== row);
  onInput();
}

function sendOffer() {
  clearTimeout(offerTimer.value);
  offerTimer.value = 0;
  const typed = amount.value.trim();
  if (!DIGITS.test(typed)) {
    refusal.value = fill(t.value.digits, { max: deal.value.max });
    return;
  }
  const unreadable = rows.value.find((row) => !DIGITS.test(row.text.trim()));
  if (unreadable) {
    refusal.value = fill(t.value.pieces_digits, { name: unreadable.name, max: countCarried(unreadable.key) });
    return;
  }

  const gold = Number(typed);
  if (gold !== deal.value.mine.gold) props.bridge.offer(gold);
  sendItems();
}

// Only what changed, with none for a stack that left the offer.
function sendItems() {
  const wanted = new Map(deal.value.mine.items.map((item) => [item.key, 0]));
  for (const row of rows.value) wanted.set(row.key, readCount(row));
  for (const [key, count] of wanted) {
    const offered = deal.value.mine.items.find((item) => item.key === key)?.count ?? 0;
    if (count !== offered) props.bridge.offerItem(key, count);
  }
}

function confirm() {
  if (canConfirm.value) props.bridge.confirm(deal.value.version);
}

function cancel() {
  if (deal.value && !done.value) props.bridge.cancel();
}

function onKeydown(event) {
  if (!deal.value || isComposing(event)) return;
  if (isHeldPress(event)) {
    event.preventDefault();
    return;
  }
  if (event.key === "Escape") {
    event.preventDefault();
    cancel();
  }
}

function onFieldEnter(event) {
  event.preventDefault();
  if (offerTimer.value) sendOffer();
  else confirm();
}

onMounted(() => {
  props.bridge.onState(onState);
  props.bridge.onRefused((text) => (refusal.value = text));
  props.bridge.onDone((text) => (done.value = text));
  props.bridge.onClosed(close);
  window.addEventListener("keydown", onKeydown);
  props.bridge.signalReady();
});

onUnmounted(() => {
  window.removeEventListener("keydown", onKeydown);
  close();
});
</script>

<template>
  <div v-if="deal" class="exchange">
    <RpPanel class="exchange__panel" :title="fill(t.title, { name: deal.other })">
      <div class="exchange__sides">
        <CarriedItems :items="available" :t="t" :describe="describe" :disabled="!!done" @add="addPiece" />

        <section class="exchange__side" :aria-label="t.yours">
          <h2 class="exchange__heading">{{ t.yours }}</h2>
          <RpField
            ref="field"
            v-model="amount"
            :label="t.gold"
            inputmode="numeric"
            :maxlength="MAX_DIGITS"
            :disabled="!!done"
            :aria-describedby="carriedId"
            @input="onInput"
            @keydown.enter="onFieldEnter"
          />
          <p :id="carriedId" class="exchange__carried">{{ fill(t.carried, { count: deal.carried }) }}</p>
          <ul v-if="rows.length" class="exchange__list">
            <li v-for="row in rows" :key="row.key" class="exchange__offered">
              <span class="exchange__name">{{ describe(row) }}</span>
              <div class="exchange__pieces" role="group" :aria-label="describe(row)">
                <RpButton class="exchange__step" :aria-label="fill(t.fewer, { name: describe(row) })" :disabled="!!done" @click="changePieces(row, -1)">−</RpButton>
                <input
                  v-model="row.text"
                  class="exchange__count"
                  inputmode="numeric"
                  spellcheck="false"
                  :maxlength="MAX_DIGITS"
                  :aria-label="fill(t.pieces, { name: describe(row) })"
                  :disabled="!!done"
                  @input="onInput"
                  @keydown.enter="onFieldEnter"
                >
                <RpButton
                  class="exchange__step"
                  :aria-label="fill(t.more, { name: describe(row) })"
                  :disabled="!!done || readCount(row) >= countCarried(row.key)"
                  @click="changePieces(row, 1)"
                >+</RpButton>
              </div>
            </li>
          </ul>
          <p v-else class="exchange__none">{{ t.no_items }}</p>
          <p class="exchange__mark" :class="{ 'exchange__mark--yes': deal.mine.confirmed || done }">
            {{ deal.mine.confirmed || done ? t.confirmed : t.not_confirmed }}
          </p>
        </section>

        <section class="exchange__side exchange__side--theirs" :aria-label="fill(t.theirs, { name: deal.other })">
          <h2 class="exchange__heading">{{ fill(t.theirs, { name: deal.other }) }}</h2>
          <p class="exchange__gold"><RpIcon name="coins" class="exchange__coin" /><span>{{ fill(t.amount, { count: deal.theirs.gold }) }}</span></p>
          <ul v-if="deal.theirs.items.length" class="exchange__list">
            <li v-for="item in deal.theirs.items" :key="item.key" class="exchange__line">
              <span class="exchange__name">{{ describe(item) }}</span>
              <span class="exchange__times">{{ fill(t.times, { count: item.count }) }}</span>
            </li>
          </ul>
          <p v-else class="exchange__none">{{ t.no_items }}</p>
          <p class="exchange__mark" :class="{ 'exchange__mark--yes': deal.theirs.confirmed || done }">
            {{ deal.theirs.confirmed || done ? t.confirmed : t.not_confirmed }}
          </p>
        </section>
      </div>

      <p class="exchange__status" :class="{ 'exchange__status--refused': refusal && !done, 'exchange__status--done': done }" role="status">
        {{ done || refusal || t.hint }}
      </p>

      <div class="exchange__buttons">
        <RpButton :disabled="!!done" @click="cancel">{{ t.cancel }}</RpButton>
        <RpButton variant="primary" :disabled="!canConfirm" @click="confirm">{{ confirmLabel }}</RpButton>
      </div>
    </RpPanel>
  </div>
</template>
