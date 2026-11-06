<script setup>
import { computed, nextTick, onMounted, onUnmounted, ref, useTemplateRef } from "vue";
import { RpButton, RpChip, RpField, RpIcon, RpPanel } from "../rp/index.js";
import { BANK_MOST_AT_ONCE } from "../shared/limits.js";
import { fill, usePageTexts } from "../shared/locale.js";
import { isComposing, isHeldPress } from "../shared/press.js";

const props = defineProps({ bridge: { type: Object, required: true } });

const WHOLE = /^\d+$/;

const { locale, t } = usePageTexts(props.bridge, "bank");

// The account as the server last sent it: {balance, gold}. Null while no banker's window is open.
const account = ref(null);
const amount = ref("");
const problem = ref(null);
const answer = ref(null);
const busy = ref(false);
const field = useTemplateRef("field");

const status = computed(() => (busy.value ? t.value.waiting : (answer.value?.text ?? t.value.hint)));

const showGold = (count) => fill(t.value.gold, { count: count.toLocaleString(locale.value) });

// The amount typed as a whole number the server takes, null otherwise.
function readAmount() {
  const text = amount.value.trim();
  if (!WHOLE.test(text)) return null;
  const wanted = Number(text);
  return wanted >= 1 && wanted <= BANK_MOST_AT_ONCE ? wanted : null;
}

// Null when the amount may go to the server. Most: the gold there is to move, refused with the text under tooMuch.
function check(wanted, most, tooMuch) {
  if (wanted === null) return fill(t.value.amount_invalid, { max: BANK_MOST_AT_ONCE.toLocaleString(locale.value) });
  return wanted > most ? fill(t.value[tooMuch], { count: most.toLocaleString(locale.value) }) : null;
}

function move(most, tooMuch, send) {
  if (busy.value) return;
  const wanted = readAmount();
  problem.value = check(wanted, most, tooMuch);
  if (problem.value) {
    field.value.focus();
    return;
  }

  busy.value = true;
  answer.value = null;
  send(wanted);
}

const deposit = () => move(account.value.gold, "too_poor", props.bridge.deposit);
const withdraw = () => move(account.value.balance, "too_little", props.bridge.withdraw);

function fillIn(sum) {
  amount.value = String(Math.min(sum, BANK_MOST_AT_ONCE));
  problem.value = null;
  field.value.focus();
}

async function onState(view) {
  const opening = !account.value;
  account.value = view;
  if (!opening) return;

  amount.value = "";
  problem.value = null;
  answer.value = null;
  busy.value = false;
  await nextTick();
  field.value.focus();
}

function onAnswer(text, done) {
  busy.value = false;
  answer.value = { text, done };
  if (done) amount.value = "";
}

function close() {
  account.value = null;
  answer.value = null;
  busy.value = false;
}

function leave() {
  if (account.value) props.bridge.close();
}

function onKeydown(event) {
  if (!account.value || isComposing(event)) return;
  if (isHeldPress(event)) {
    event.preventDefault();
    return;
  }

  if (event.key === "Escape") {
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
  <div v-if="account" class="bank">
    <RpPanel class="bank__panel" variant="letter" :title="t.title">
      <p class="bank__lead">{{ t.lead }}</p>

      <dl class="bank__sums">
        <div class="bank__sum" data-sum="balance">
          <dt>{{ t.balance }}</dt>
          <dd>{{ showGold(account.balance) }}</dd>
        </div>
        <div class="bank__sum" data-sum="gold">
          <dt><RpIcon name="coins" class="bank__coin" />{{ t.purse }}</dt>
          <dd>{{ showGold(account.gold) }}</dd>
        </div>
      </dl>

      <form class="bank__form" novalidate @submit.prevent>
        <RpField
          ref="field"
          v-model="amount"
          :label="t.amount"
          :invalid="!!problem"
          inputmode="numeric"
          autocomplete="off"
          :maxlength="String(BANK_MOST_AT_ONCE).length"
          data-field="amount"
        />
        <div class="bank__fill" role="group" :aria-label="t.fill">
          <RpChip :disabled="account.gold === 0" data-fill="gold" @click="fillIn(account.gold)">{{ t.all_carried }}</RpChip>
          <RpChip :disabled="account.balance === 0" data-fill="balance" @click="fillIn(account.balance)">{{ t.all_banked }}</RpChip>
        </div>
        <p v-if="problem" class="bank__problem" role="alert">{{ problem }}</p>

        <div class="bank__acts">
          <RpButton variant="primary" :disabled="busy" :aria-busy="busy" data-action="deposit" @click="deposit">{{ t.deposit }}</RpButton>
          <RpButton :disabled="busy" :aria-busy="busy" data-action="withdraw" @click="withdraw">{{ t.withdraw }}</RpButton>
        </div>
      </form>

      <p class="bank__status" :class="answer && !busy && (answer.done ? 'bank__status--done' : 'bank__status--refused')" role="status">
        {{ status }}
      </p>

      <div class="bank__footer">
        <RpButton data-action="close" @click="leave">{{ t.close }}</RpButton>
      </div>
    </RpPanel>
  </div>
</template>
