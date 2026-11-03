<script setup>
import { computed, nextTick, onMounted, onUnmounted, ref, shallowRef, useId, useTemplateRef } from "vue";
import { RpButton, RpField, RpIcon, RpPanel } from "../rp/index.js";
import { fill, usePageTexts } from "../shared/locale.js";
import { isComposing, isHeldPress } from "../shared/press.js";

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
const confirmLabel = computed(() => {
  if (done.value) return t.value.done;
  if (deal.value?.mine.confirmed) return fill(t.value.waiting, { name: deal.value.other });
  if (secondsLeft.value > 0) return fill(t.value.confirm_in, { seconds: secondsLeft.value });
  return t.value.confirm;
});

async function onState(view) {
  const opening = !deal.value;
  deal.value = view;
  confirmAt = Date.now() + view.confirmInMs;
  now.value = Date.now();
  // What the player is typing stays theirs until it goes.
  if (!offerTimer.value) amount.value = String(view.mine.gold);
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

function sendOffer() {
  clearTimeout(offerTimer.value);
  offerTimer.value = 0;
  const typed = amount.value.trim();
  if (!DIGITS.test(typed)) {
    refusal.value = fill(t.value.digits, { max: deal.value.max });
    return;
  }
  props.bridge.offer(typed === "" ? 0 : Number(typed));
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
          <p class="exchange__mark" :class="{ 'exchange__mark--yes': deal.mine.confirmed || done }">
            {{ deal.mine.confirmed || done ? t.confirmed : t.not_confirmed }}
          </p>
        </section>

        <section class="exchange__side exchange__side--theirs" :aria-label="fill(t.theirs, { name: deal.other })">
          <h2 class="exchange__heading">{{ fill(t.theirs, { name: deal.other }) }}</h2>
          <p class="exchange__gold"><RpIcon name="coins" class="exchange__coin" /><span>{{ fill(t.amount, { count: deal.theirs.gold }) }}</span></p>
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
