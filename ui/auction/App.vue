<script setup>
import { computed, nextTick, onMounted, onUnmounted, ref, useId, useTemplateRef } from "vue";
import { makeTabId, RpButton, RpIcon, RpPanel, RpTabs } from "../rp/index.js";
import { fill, usePageTexts } from "../shared/locale.js";
import { isComposing, isHeldPress } from "../shared/press.js";
import BrowseTab from "./BrowseTab.vue";
import CollectTab from "./CollectTab.vue";
import { describeItem, describeLeft } from "./lots.js";
import MineTab from "./MineTab.vue";
import SellTab from "./SellTab.vue";
import { TABS } from "./tabs.js";

const props = defineProps({ bridge: { type: Object, required: true } });

const TICK_MS = 1000;

const { locale, t } = usePageTexts(props.bridge, "auction");
const panelId = useId();
const browseTab = useTemplateRef("browseTab");

// The window as the server last sent it (AuctionView.cs). Null while no auctioneer's window is open.
const view = ref(null);
const tab = ref("browse");
const answer = ref(null);
const busy = ref(false);
const searching = ref(false);
const receivedAt = ref(0);
const now = ref(0);
let ticker;

const tabs = computed(() => TABS.map((each) => ({
  ...each,
  label: each.id === "collect" && view.value?.parcels.length
    ? fill(t.value["tab.collect_count"], { count: view.value.parcels.length })
    : t.value[`tab.${each.id}`],
})));

const status = computed(() => {
  if (busy.value) return t.value.waiting;
  if (searching.value) return t.value.searching;
  return answer.value?.text ?? t.value.hint;
});

const showNumber = (count) => count.toLocaleString(locale.value);
const showGold = (count) => fill(t.value.gold, { count: showNumber(count) });
const describe = (item) => describeItem(t.value, item);

// What a lot has left, counted on from when the window got it.
const showLeft = (lot) => describeLeft(t.value, lot.endsInMs - (now.value - receivedAt.value));

async function onState(next) {
  const opening = !view.value;
  view.value = next;
  receivedAt.value = Date.now();
  now.value = receivedAt.value;
  searching.value = false;
  if (!opening) return;

  tab.value = "browse";
  answer.value = null;
  busy.value = false;
  await nextTick();
  browseTab.value?.focus();
}

function onAnswer(text, done) {
  busy.value = false;
  searching.value = false;
  answer.value = { text, done };
}

function close() {
  view.value = null;
  answer.value = null;
  busy.value = false;
  searching.value = false;
}

function leave() {
  if (view.value) props.bridge.close();
}

// A request the house answers: the window waits for the answer and sends no second one meanwhile.
function request(send) {
  if (busy.value) return;
  busy.value = true;
  answer.value = null;
  send();
}

function search(text, category, page) {
  searching.value = true;
  props.bridge.search(text, category, page);
}

const bid = (lot, amount) => request(() => props.bridge.bid(lot, amount));
const buy = (lot, price) => request(() => props.bridge.buy(lot, price));
const post = (offer) => request(() => props.bridge.post(offer.item, offer.count, offer.startPrice, offer.buyoutPrice, offer.hours));
const withdraw = (lot) => request(() => props.bridge.withdraw(lot));
const collect = (parcel) => request(() => props.bridge.collect(parcel));

function onKeydown(event) {
  if (!view.value || isComposing(event)) return;
  if (isHeldPress(event)) {
    event.preventDefault();
    return;
  }

  if (event.key === "Escape" || event.keyCode === 27) {
    event.preventDefault();
    leave();
  }
}

onMounted(() => {
  props.bridge.onState(onState);
  props.bridge.onAnswer(onAnswer);
  props.bridge.onClosed(close);
  window.addEventListener("keydown", onKeydown);
  ticker = setInterval(() => { now.value = Date.now(); }, TICK_MS);
  props.bridge.signalReady();
});

onUnmounted(() => {
  clearInterval(ticker);
  window.removeEventListener("keydown", onKeydown);
});
</script>

<template>
  <div v-if="view" class="auction">
    <RpPanel class="auction__panel" variant="letter" :title="t.title">
      <p class="auction__purse">
        <RpIcon name="coins" class="auction__coin" />
        <span>{{ t.purse }}</span>
        <strong data-purse>{{ showGold(view.gold) }}</strong>
      </p>

      <div class="auction__body">
        <!-- The tabs turn only from the tab list: Q and E elsewhere are letters typed into the window's fields. -->
        <RpTabs v-model="tab" :tabs="tabs" :label="t.tabs" :controls="panelId" size="small" />

        <div :id="panelId" class="auction__content" role="tabpanel" :aria-labelledby="makeTabId(panelId, tab)">
          <BrowseTab
            v-if="tab === 'browse'"
            ref="browseTab"
            :view="view"
            :t="t"
            :busy="busy"
            :show-left="showLeft"
            :show-number="showNumber"
            :describe="describe"
            @search="search"
            @bid="bid"
            @buy="buy"
          />
          <SellTab v-else-if="tab === 'sell'" :view="view" :t="t" :busy="busy" :show-number="showNumber" :describe="describe" @post="post" />
          <MineTab v-else-if="tab === 'mine'" :view="view" :t="t" :busy="busy" :show-left="showLeft" :show-number="showNumber" :describe="describe" @withdraw="withdraw" />
          <CollectTab v-else :view="view" :t="t" :busy="busy" :show-gold="showGold" :describe="describe" @collect="collect" />
        </div>
      </div>

      <p class="auction__status" :class="answer && !busy && (answer.done ? 'auction__status--done' : 'auction__status--refused')" role="status">
        {{ status }}
      </p>

      <div class="auction__footer">
        <RpButton data-action="close" @click="leave">{{ t.close }}</RpButton>
      </div>
    </RpPanel>
  </div>
</template>
