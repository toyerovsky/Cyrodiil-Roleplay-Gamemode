<script setup>
import { computed, nextTick, onMounted, onUnmounted, ref, useTemplateRef } from "vue";
import { RpButton, RpIcon, RpPanel } from "../rp/index.js";
import { fill, usePageTexts } from "../shared/locale.js";
import { isComposing, isHeldPress } from "../shared/press.js";

const props = defineProps({ bridge: { type: Object, required: true } });

const { locale, t } = usePageTexts(props.bridge, "plot");

// The plot as the server last sent it, null while no sign's window is open.
const plot = ref(null);
const answer = ref(null);
const busy = ref(false);
const panel = useTemplateRef("panel");

const forSale = computed(() => plot.value?.standing === "ForSale");
const tooMany = computed(() => forSale.value && plot.value.owned >= plot.value.mostOwned);
const tooPoor = computed(() => forSale.value && plot.value.gold < plot.value.price);
const canBuy = computed(() => forSale.value && !tooMany.value && !tooPoor.value && !busy.value);
const lead = computed(() => t.value[`lead.${plot.value.standing}`]);
const status = computed(() => (busy.value ? t.value.waiting : (answer.value?.text ?? t.value.hint)));

const showGold = (count) => fill(t.value.gold, { count: count.toLocaleString(locale.value) });
const showMetres = (metres) => metres.toLocaleString(locale.value, { maximumFractionDigits: 1 });

const buyLabel = computed(() => {
  if (tooMany.value) return t.value.too_many;
  if (tooPoor.value) return t.value.too_poor;
  return fill(t.value.buy, { price: plot.value.price.toLocaleString(locale.value) });
});

function focusFirst() {
  const root = panel.value?.$el;
  (root?.querySelector('[data-action="buy"]:not(:disabled)') ?? root?.querySelector('[data-action="close"]'))?.focus();
}

function buy() {
  if (!canBuy.value) return;
  busy.value = true;
  answer.value = null;
  props.bridge.buy();
}

// A button that went away or shut under the focus hands it on, so the keys keep working.
async function keepFocus() {
  await nextTick();
  const focused = document.activeElement;
  if (!panel.value?.$el.contains(focused) || focused.disabled) focusFirst();
}

async function onState(view) {
  const opening = !plot.value;
  plot.value = view;
  if (!opening) {
    await keepFocus();
    return;
  }

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
  plot.value = null;
  answer.value = null;
  busy.value = false;
}

function leave() {
  if (plot.value) props.bridge.close();
}

function onKeydown(event) {
  if (!plot.value || isComposing(event)) return;
  if (isHeldPress(event)) {
    event.preventDefault();
    return;
  }

  if (event.key !== "Escape") return;
  event.preventDefault();
  leave();
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
  <div v-if="plot" class="plot">
    <RpPanel ref="panel" class="plot__panel" variant="board" :title="fill(t.title, { id: plot.id })">
      <p class="plot__lead" :data-standing="plot.standing">{{ lead }}</p>

      <dl class="plot__facts">
        <div class="plot__fact" data-fact="kind">
          <dt>{{ t.kind_label }}</dt>
          <dd>{{ t[`kind.${plot.kind}`] }}</dd>
        </div>
        <div class="plot__fact" data-fact="size">
          <dt>{{ t.size_label }}</dt>
          <dd>
            {{ fill(t.size, { width: plot.width, depth: plot.depth, widthMetres: showMetres(plot.widthMetres), depthMetres: showMetres(plot.depthMetres) }) }}
          </dd>
        </div>
        <div class="plot__fact" data-fact="height">
          <dt>{{ t.height_label }}</dt>
          <dd>{{ fill(t.metres, { metres: plot.heightMetres }) }}</dd>
        </div>
        <div class="plot__fact" data-fact="price">
          <dt><RpIcon name="coins" class="plot__mark" />{{ t.price_label }}</dt>
          <dd :class="{ 'plot__dear': tooPoor }">{{ showGold(plot.price) }}</dd>
        </div>
        <div class="plot__fact" data-fact="tax">
          <dt>{{ t.tax_label }}</dt>
          <dd>{{ showGold(plot.weeklyTax) }}</dd>
        </div>
        <div class="plot__fact" data-fact="gold">
          <dt><RpIcon name="coins" class="plot__mark" />{{ t.purse }}</dt>
          <dd>{{ showGold(plot.gold) }}</dd>
        </div>
      </dl>

      <p v-if="tooMany" class="plot__note">{{ fill(t.most_note, { most: plot.mostOwned }) }}</p>

      <p class="plot__status" :class="answer && !busy && (answer.bought ? 'plot__status--bought' : 'plot__status--refused')" role="status">
        {{ status }}
      </p>

      <div class="plot__footer">
        <RpButton
          v-if="forSale"
          data-action="buy"
          :variant="canBuy ? 'primary' : 'default'"
          :disabled="!canBuy"
          :aria-busy="busy"
          @click="buy"
        >
          {{ buyLabel }}
        </RpButton>
        <RpButton data-action="close" @click="leave">{{ t.close }}</RpButton>
      </div>
    </RpPanel>
  </div>
</template>
