<script setup>
import { computed, nextTick, onMounted, onUnmounted, ref, useId, useTemplateRef } from "vue";
import { RpButton, RpIcon, RpPanel } from "../rp/index.js";
import { fill, usePageTexts } from "../shared/locale.js";
import { isComposing, isHeldPress } from "../shared/press.js";

const props = defineProps({ bridge: { type: Object, required: true } });

const { locale, t } = usePageTexts(props.bridge, "stable");

// The stable as the server last sent it: {horses: [{id, price, owned}], gold}. Null while no stablemaster's window is open.
const stable = ref(null);
// The horse that would take the place of the one owned, while the player is asked to say so.
const replacing = ref(null);
const answer = ref(null);
const busy = ref(false);
const panel = useTemplateRef("panel");
const confirmId = useId();

const owned = computed(() => stable.value?.horses.find((horse) => horse.owned) ?? null);
const status = computed(() => (busy.value ? t.value.waiting : (answer.value?.text ?? t.value.hint)));

const nameOf = (horse) => t.value[`horse.${horse.id}`] ?? horse.id;
const showGold = (count) => fill(t.value.gold, { count: count.toLocaleString(locale.value) });
const isDear = (horse) => !horse.owned && horse.price > stable.value.gold;
const isOffered = (horse) => !horse.owned && !isDear(horse);
const canBuy = (horse) => !busy.value && !replacing.value && isOffered(horse);

function readAction(horse) {
  if (horse.owned) return t.value.owned;
  return isDear(horse) ? t.value.too_poor : t.value.buy;
}

// The first horse there is to buy, or Leave when there is none.
function focusFirst() {
  const root = panel.value?.$el;
  (root?.querySelector("[data-horse] button:not(:disabled)") ?? root?.querySelector('[data-action="close"]'))?.focus();
}

// A button that went disabled under the focus hands it on, so the keys keep working.
async function keepFocus() {
  await nextTick();
  const root = panel.value?.$el;
  const focused = document.activeElement;
  if (!root?.contains(focused) || focused.disabled) focusFirst();
}

function pick(horse) {
  if (!canBuy(horse)) return;
  if (!owned.value) {
    buy(horse);
    return;
  }

  replacing.value = horse;
  nextTick(() => panel.value?.$el.querySelector('[data-action="confirm"]')?.focus());
}

function buy(horse) {
  busy.value = true;
  answer.value = null;
  replacing.value = null;
  props.bridge.buy(horse.id);
}

function cancel() {
  const id = replacing.value?.id;
  replacing.value = null;
  nextTick(() => panel.value?.$el.querySelector(`[data-horse="${id}"] button`)?.focus());
}

async function onState(view) {
  const opening = !stable.value;
  stable.value = view;
  if (replacing.value && !view.horses.some((horse) => horse.id === replacing.value.id && isOffered(horse))) replacing.value = null;
  if (!opening) {
    await keepFocus();
    return;
  }

  replacing.value = null;
  answer.value = null;
  busy.value = false;
  await nextTick();
  focusFirst();
}

function onAnswer(text, bought) {
  busy.value = false;
  answer.value = { text, bought };
  keepFocus();
}

function close() {
  stable.value = null;
  replacing.value = null;
  answer.value = null;
  busy.value = false;
}

function leave() {
  if (stable.value) props.bridge.close();
}

function onKeydown(event) {
  if (!stable.value || isComposing(event)) return;
  if (isHeldPress(event)) {
    event.preventDefault();
    return;
  }

  if (event.key !== "Escape") return;
  event.preventDefault();
  if (replacing.value) cancel();
  else leave();
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
  <div v-if="stable" class="stable">
    <RpPanel ref="panel" class="stable__panel" variant="letter" :title="t.title">
      <p class="stable__lead">{{ t.lead }}</p>

      <dl class="stable__sums">
        <div class="stable__sum" data-sum="horse">
          <dt><RpIcon name="horse" class="stable__mark" />{{ t.yours }}</dt>
          <dd>{{ owned ? nameOf(owned) : t.none }}</dd>
        </div>
        <div class="stable__sum" data-sum="gold">
          <dt><RpIcon name="coins" class="stable__mark" />{{ t.purse }}</dt>
          <dd>{{ showGold(stable.gold) }}</dd>
        </div>
      </dl>

      <p v-if="owned" class="stable__note">{{ fill(t.replace_note, { horse: nameOf(owned) }) }}</p>

      <ul class="stable__horses" :aria-label="t.horses">
        <li
          v-for="horse in stable.horses"
          :key="horse.id"
          class="stable__horse"
          :class="{ 'stable__horse--owned': horse.owned, 'stable__horse--dear': isDear(horse) }"
          :data-horse="horse.id"
        >
          <RpIcon name="horse" class="stable__icon" />
          <span class="stable__name">{{ nameOf(horse) }}</span>
          <span class="stable__price">{{ showGold(horse.price) }}</span>
          <RpButton
            class="stable__buy"
            :variant="canBuy(horse) ? 'primary' : 'default'"
            :disabled="!canBuy(horse)"
            :aria-busy="busy"
            :aria-label="fill(t.action, { action: readAction(horse), horse: nameOf(horse) })"
            @click="pick(horse)"
          >
            {{ readAction(horse) }}
          </RpButton>
        </li>
      </ul>

      <section v-if="replacing" class="stable__confirm" role="alertdialog" :aria-labelledby="confirmId">
        <p :id="confirmId" class="stable__question">
          {{ fill(t.confirm, { horse: nameOf(replacing), price: replacing.price.toLocaleString(locale), owned: nameOf(owned) }) }}
        </p>
        <div class="stable__acts">
          <RpButton variant="primary" data-action="confirm" @click="buy(replacing)">{{ t.confirm_yes }}</RpButton>
          <RpButton data-action="cancel" @click="cancel">{{ t.confirm_no }}</RpButton>
        </div>
      </section>

      <p class="stable__status" :class="answer && !busy && (answer.bought ? 'stable__status--bought' : 'stable__status--refused')" role="status">
        {{ status }}
      </p>

      <div class="stable__footer">
        <RpButton data-action="close" @click="leave">{{ t.close }}</RpButton>
      </div>
    </RpPanel>
  </div>
</template>
