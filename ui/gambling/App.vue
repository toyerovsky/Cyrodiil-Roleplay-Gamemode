<script setup>
import { computed, nextTick, onMounted, onUnmounted, ref, useTemplateRef } from "vue";
import { RpButton, RpChip, RpField, RpPanel } from "../rp/index.js";
import { fill, usePageTexts } from "../shared/locale.js";
import { isComposing, isHeldPress } from "../shared/press.js";
import Rules from "./Rules.vue";
import TableBoard from "./TableBoard.vue";
import { seatName, viewerSeats } from "./table.js";
import { Sound } from "../dice/board/sound.js";
import medallion from "../rp/art/gambling.webp";

const props = defineProps({ bridge: { type: Object, required: true } });
const { t } = usePageTexts(props.bridge, "gambling");
const view = ref(null);
const live = ref(false);
const sound = new Sound();
const shownThrows = new Set();
const rules = ref(false);
const mode = ref("Poker");
const stake = ref("40");
const opponent = ref(null);
const raise = ref("1");
const mask = ref(0);
let pendingMask = null;
const waiting = ref(false);
const invited = ref(false);
const refusal = ref("");
const now = ref(Date.now());
const deadline = ref(0);
const receivedAt = ref(0);
const heading = useTemplateRef("heading");
let ticker;
// Whether the focus was last moved with Tab, not with the mouse.
let tabbed = false;
const onPointer = () => { tabbed = false; };
const match = computed(() => view.value?.match);
const seconds = computed(() => Math.max(0, Math.ceil((deadline.value - now.value) / 1000)));
const validOpponent = computed(() => view.value?.opponents.some((person) => person.number === opponent.value && !person.refusal));
const validStake = computed(() => /^\d+$/.test(String(stake.value)) && Number(stake.value) >= view.value.minStake && Number(stake.value) <= view.value.maxStake);
const validRaise = computed(() => /^\d+$/.test(String(raise.value)) && Number(raise.value) >= 1 && Number(raise.value) <= match.value?.raiseLimit);
const choosing = computed(() => match.value?.phase === "Reroll" && !match.value.confirmed && !match.value.busy && !waiting.value && seconds.value > 0);
const acting = computed(() => match.value?.yourTurn && !match.value.busy && !waiting.value && seconds.value > 0);
const selected = computed(() => [0, 1, 2, 3, 4].filter((index) => mask.value & (1 << index)).length);
const decision = computed(() => {
  const current = match.value;
  if (!current) return "";
  if (current.phase === "Rolling") return current.turnSeat < 0 ? t.value.Rolling : fill(t.value.rolling_name, { name: seatName(current, current.turnSeat) });
  if (current.busy || waiting.value) return t.value.waiting;
  if (current.result) return current.winner < 0 ? current.result : `${seatName(current, current.winner === 0 ? current.seat : 1 - current.seat)}: ${current.result}`;
  if (current.phase === "Raise" || current.phase === "Call")
    return fill(t.value[`${current.phase}.${current.yourTurn ? "you" : "other"}`], { amount: current.callAmount, name: current.yourTurn ? current.you : current.opponent, opponent: current.opponent });
  if (current.phase === "Reroll" && current.confirmed) return t.value.confirmed;
  if (current.phase === "RoundResult" && current.roundWinner >= 0) return fill(t.value.round_winner, { name: seatName(current, current.roundWinner) });
  return t.value[current.phase];
});

async function onState(next) {
  const opening = !view.value;
  const before = match.value;
  if (!before || next.match?.id !== before.id || next.match?.round !== before.round || next.match?.replay !== before.replay || next.match?.phase !== before.phase)
  {
    mask.value = 0;
    pendingMask = null;
  }
  if (next.match?.id !== before?.id) shownThrows.clear();
  if ((opening || next.rules) && next.match)
    next.match.throws.forEach((thrown) => shownThrows.add(`${next.match.id}:${thrown.id}`));
  if (opening || (before && !next.match)) mode.value = "Poker";
  live.value = !opening;
  view.value = next;
  rules.value = opening || next.rules ? next.rules : rules.value;
  if (opening && !next.match) stake.value = String(Math.max(next.minStake, Math.min(next.maxStake, Number(stake.value))));
  if (next.opponent != null) opponent.value = next.opponent;
  else if (!next.opponents.some((person) => person.number === opponent.value && !person.refusal))
    opponent.value = next.opponents.find((person) => !person.refusal)?.number ?? null;
  now.value = Date.now();
  receivedAt.value = now.value;
  deadline.value = now.value + (next.match?.leftMs ?? 0);
  if (next.match?.picks) {
    const acknowledged = next.match.picks[next.match.seat];
    if (acknowledged === pendingMask) pendingMask = null;
    if (pendingMask === null) mask.value = acknowledged;
  }
  waiting.value = false;
  refusal.value = "";
  if (next.match) invited.value = false;
  if (opening) {
    await nextTick();
    heading.value?.focus();
  }
}

function invite() {
  if (!validStake.value || !validOpponent.value || waiting.value || invited.value) return;
  waiting.value = true;
  invited.value = true;
  refusal.value = "";
  props.bridge.invite(opponent.value, Number(stake.value), mode.value);
}

function act(action, amount = 0) {
  // The ticker refreshes `now` four times a second, so the deadline is checked against the clock itself.
  if (!match.value || waiting.value || match.value.busy || Date.now() >= deadline.value) return;
  waiting.value = true;
  refusal.value = "";
  props.bridge.act(match.value.id, match.value.version, action, amount);
}

function pick(index) {
  if (!choosing.value || Date.now() >= deadline.value) return;
  mask.value ^= 1 << index;
  pendingMask = mask.value;
  props.bridge.act(match.value.id, match.value.version, "Select", mask.value);
}

function takeAnimation(id) {
  const key = `${match.value.id}:${id}`;
  const animate = live.value && !shownThrows.has(key);
  shownThrows.add(key);
  return animate;
}

function toggleRules() {
  if (match.value) match.value.throws.forEach((thrown) => shownThrows.add(`${match.value.id}:${thrown.id}`));
  rules.value = !rules.value;
}

function onKey(event) {
  if (!view.value || isComposing(event)) return;
  sound.wake();
  if (isHeldPress(event)) { event.preventDefault(); return; }
  // Also in the rules and in a field, which leave the keys below to themselves.
  if (event.key === "Tab" || event.keyCode === 9) tabbed = true;
  if ((event.key === "Escape" || event.keyCode === 27)) { event.preventDefault(); props.bridge.close(); return; }
  if (rules.value || /^(INPUT|TEXTAREA|SELECT)$/.test(event.target?.tagName)) return;
  // CEF sends keyCode without code, including the number row and Space.
  const number = /^[1-5]$/.test(event.key) ? Number(event.key) : event.keyCode >= 49 && event.keyCode <= 53 ? event.keyCode - 48 : 0;
  if (choosing.value && number) { event.preventDefault(); pick(number - 1); }
  // A button reached with Tab takes Space and Enter itself. One only clicked leaves them to the confirmation.
  const onButton = tabbed && event.target?.tagName === "BUTTON";
  if (choosing.value && !onButton && (event.key === " " || event.key === "Enter" || event.keyCode === 32 || event.keyCode === 13)) {
    event.preventDefault();
    act("Pick", mask.value);
  }
}

onMounted(() => {
  props.bridge.onState(onState);
  props.bridge.onRefused((text) => { refusal.value = text; waiting.value = false; invited.value = false; });
  props.bridge.onClosed(() => { view.value = null; waiting.value = false; invited.value = false; });
  window.addEventListener("keydown", onKey);
  window.addEventListener("pointerdown", onPointer, true);
  ticker = setInterval(() => { now.value = Date.now(); }, 250);
  props.bridge.signalReady();
});
onUnmounted(() => { sound.ctx?.close(); clearInterval(ticker); window.removeEventListener("keydown", onKey); window.removeEventListener("pointerdown", onPointer, true); });
</script>

<template>
  <div v-if="view" class="gambling" :class="{ 'gambling--match': match && !rules }" @click.capture="sound.wake()">
    <RpPanel class="gambling__panel" :class="{ 'gambling__panel--match': match && !rules }" :title="t.title">
      <template #title><img class="gambling__medallion" :src="medallion" alt="">{{ t.title }}</template>
      <div class="gambling__content">
        <h2 ref="heading" class="gambling__heading" tabindex="-1">{{ rules ? t.rules : match ? t[match.mode] : t.pick_game }}</h2>
        <Rules v-if="rules" :t="t" :view="view" />
        <template v-else-if="match">
          <div class="gambling__summary">
            <p>{{ fill(t.round, { round: match.round, you: match.wins[0], other: match.wins[1] }) }}</p>
            <p v-if="match.replay">{{ fill(t.replay, { replay: match.replay }) }}</p>
            <div class="gambling__pot"><span>{{ t.pot }}</span><b>{{ fill(t.gold, { amount: match.pot }) }}</b></div>
          </div>
          <div class="gambling__table-layout">
            <div class="gambling__hands">
              <section v-for="seat in viewerSeats(match.seat)" :key="seat" class="gambling__hand rp-plate" :data-seat="seat" :aria-label="seat === match.seat ? t.you : t.opponent">
                <div class="gambling__seat-info">
                  <span class="gambling__seat-label">{{ seat === match.seat ? t.you : t.opponent }}</span>
                  <h3>{{ seatName(match, seat) }}</h3>
                  <strong v-if="match.phase !== 'Rolling'">{{ seat === match.seat ? match.yourRank || fill(t.total, { total: match.yourHand.reduce((sum, die) => sum + die, 0) }) : match.opponentRank || fill(t.total, { total: match.opponentHand.reduce((sum, die) => sum + die, 0) }) }}</strong>
                  <p>{{ fill(t.wins, { count: match.wins[seat === match.seat ? 0 : 1] }) }}</p>
                  <div class="gambling__dice" role="group" :aria-label="seatName(match, seat)">
                    <button v-for="(die, index) in (seat === match.seat ? match.yourHand : match.opponentHand)" :key="index" type="button" class="gambling__die" :class="{ 'gambling__die--picked': (seat === match.seat ? mask : match.picks[seat]) & (1 << index) }" :aria-label="fill(t.die, { number: index + 1, value: die })" :aria-pressed="Boolean((seat === match.seat ? mask : match.picks[seat]) & (1 << index))" :disabled="seat !== match.seat || !choosing" @click="pick(index)"><small>{{ index + 1 }}</small><span>{{ die }}</span></button>
                  </div>
                </div>
                <TableBoard :key="`${match.id}:${match.round}:${match.replay}:${seat}`" :match="match" :seat="seat" :label="seatName(match, seat)" :picked="match.phase === 'Reroll' ? (seat === match.seat ? mask : match.picks[seat]) : 0" :enabled="seat === match.seat && choosing" :take-animation="takeAnimation" :received-at="receivedAt" :sound="sound" @pick="pick" />
              </section>
            </div>
            <aside class="gambling__controls">
              <div class="gambling__decision" role="status"><p>{{ decision }}</p><b v-if="match.phase !== 'Finished' && match.phase !== 'Rolling'">{{ fill(t.time, { seconds }) }}</b></div>
              <div v-if="match.phase === 'Raise' && match.yourTurn" class="gambling__actions">
                <RpField v-model="raise" inputmode="numeric" maxlength="10" :label="t.raise_amount" :min="1" :max="match.raiseLimit" :invalid="!validRaise" />
                <p>{{ fill(t.raise_limit, { amount: match.raiseLimit }) }}</p>
                <RpButton variant="primary" :disabled="!acting || !validRaise" @click="act('Raise', Number(raise))">{{ t.raise }}</RpButton>
                <RpButton :disabled="!acting" @click="act('Pass')">{{ t.pass }}</RpButton>
                <RpButton :disabled="!acting" @click="act('Fold')">{{ t.fold }}</RpButton>
              </div>
              <div v-else-if="match.phase === 'Call' && match.yourTurn" class="gambling__actions">
                <RpButton variant="primary" :disabled="!acting" @click="act('Call')">{{ fill(t.call, { amount: match.callAmount }) }}</RpButton>
                <RpButton :disabled="!acting" @click="act('Fold')">{{ t.fold }}</RpButton>
              </div>
              <div v-else-if="match.phase === 'Reroll'" class="gambling__actions">
                <p>{{ fill(t.selected, { count: selected }) }}</p>
                <RpButton variant="primary" :disabled="!choosing" @click="act('Pick', mask)">{{ t.confirm }}</RpButton>
              </div>
              <RpButton v-if="match.result" @click="bridge.refresh()">{{ t.back }}</RpButton>
            </aside>
          </div>
        </template>
        <template v-else>
          <p>{{ t.lead }}</p>
          <div class="gambling__games" role="group" :aria-label="t.pick_game"><RpChip v-for="game in ['Poker', 'Favor']" :key="game" :pressed="game === mode" @click="mode = game">{{ t[game] }}</RpChip></div>
          <p>{{ t[`rules.${mode}`].replace('{rounds}', view.roundsToWin) }}</p>
          <p>{{ fill(t.range, { min: view.minStake, max: view.maxStake, cut: view.cutPercent }) }}</p>
          <form class="gambling__form" @submit.prevent="invite">
            <RpField v-model="stake" inputmode="numeric" maxlength="10" :label="t.stake" :min="view.minStake" :max="view.maxStake" :invalid="!validStake || Boolean(refusal)" />
            <p v-if="!validStake" role="alert">{{ fill(t.range, { min: view.minStake, max: view.maxStake, cut: view.cutPercent }) }}</p>
            <h2>{{ t.opponent }}</h2>
            <p v-if="!view.table">{{ t.no_table }}</p><p v-else-if="!view.opponents.length">{{ t.none }}</p>
            <div v-else class="gambling__opponents" role="group" :aria-label="t.opponent"><div v-for="person in view.opponents" :key="person.number"><RpChip :pressed="person.number === opponent" :disabled="Boolean(person.refusal)" @click="opponent = person.number">[{{ person.number }}] {{ person.name }}</RpChip><p v-if="person.refusal">{{ person.refusal }}</p></div></div>
            <p v-if="invited" role="status">{{ t.invited }}</p>
            <RpButton variant="primary" type="submit" :disabled="!view.table || !validStake || !validOpponent || waiting || invited">{{ waiting ? t.waiting : t.invite }}</RpButton>
          </form>
        </template>
        <p v-if="refusal" class="gambling__refusal" role="alert">{{ refusal }}</p>
      </div>
      <footer class="gambling__footer"><p>{{ t.hide_hint }}</p><div><RpButton @click="toggleRules">{{ rules ? t.back : t.rules }}</RpButton><RpButton v-if="!match && !rules" @click="invited = false; bridge.refresh()">{{ t.refresh }}</RpButton><RpButton @click="bridge.close()">{{ t.hide }}</RpButton></div></footer>
    </RpPanel>
  </div>
</template>
