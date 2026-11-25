<script setup>
import { computed, nextTick, onMounted, onUnmounted, reactive, ref, useId, useTemplateRef, watch } from "vue";
import { makeTabId, RpButton, RpIcon, RpPanel, RpTabs } from "../rp/index.js";
import { findPicture, findSpeciesPicture } from "../shared/fishing/pictures.js";
import { fill, usePageTexts } from "../shared/locale.js";
import { isComposing, isHeldPress, readCode } from "../shared/press.js";
import { TABS } from "./tabs.js";

const props = defineProps({ bridge: { type: Object, required: true } });

const PERCENT = 100;
const COUNTED = new Set(["Bait", "Lure"]);
const EMPTY_PICTURES = Object.freeze({ catch: "perch", shop: "worms", repair: "rod" });
const TAB_KEYS = Object.freeze({ KeyQ: -1, KeyE: 1 });

const { locale, t } = usePageTexts(props.bridge, "fisher");
const panelId = useId();
const listId = useId();
const statusId = useId();
const list = useTemplateRef("list");
const tabList = useTemplateRef("tabList");

// The window as the server last sent it (a FisherView). Null while no fisher's window is open.
const view = ref(null);
const tab = ref("catch");
// The row picked in each tab, by its item or ware key.
const picked = reactive({ catch: "", shop: "", repair: "" });
const count = ref(1);
const answer = ref(null);
const busy = ref(false);

const current = computed(() => TABS.find((each) => each.id === tab.value));
const tabs = computed(() => TABS.map((each) => ({ ...each, label: t.value[`tab.${each.id}`] })));
const rows = computed(() => (view.value ? current.value.rows(view.value) : []));
const keyOf = (row) => row[current.value.key];
const selected = computed(() => rows.value.find((row) => keyOf(row) === picked[tab.value]) ?? null);
const hint = computed(() => t.value[rows.value.length > 0 ? `hint.${tab.value}` : "hint.empty"]);
const status = computed(() => (busy.value ? t.value.waiting : (answer.value?.text ?? hint.value)));

const counted = computed(() => tab.value === "shop" && !!selected.value && COUNTED.has(selected.value.kind));
const total = computed(() => (tab.value === "shop" && selected.value ? selected.value.price * count.value : 0));
const most = computed(() => {
  if (!counted.value) return 1;
  return Math.max(1, Math.min(view.value.most, Math.floor(view.value.gold / selected.value.price)));
});
const catchWorth = computed(() => view.value?.catch.reduce((sum, row) => sum + row.price * row.count, 0) ?? 0);

const showNumber = (number) => number.toLocaleString(locale.value);
const showGold = (number) => fill(t.value.gold, { count: showNumber(number) });
const showMetres = (cm) => (cm / PERCENT).toLocaleString(locale.value, { maximumFractionDigits: 1 });
const showWeight = (row) => Number(row.weight.toFixed(1)).toLocaleString(locale.value);
const isDear = (row) => tab.value !== "catch" && row.price > view.value.gold;
const pictureOf = (row) => (tab.value === "catch" ? findSpeciesPicture(row.species) : findPicture(row.icon));
const optionId = (index) => `${listId}-${index}`;

function select(row) {
  picked[tab.value] = row ? keyOf(row) : "";
  count.value = 1;
}

// The first row, or the tabs while the tab has none.
function focusMain() {
  if (rows.value.length > 0) list.value?.focus();
  else tabList.value?.focus();
}

// A row sold or mended away hands its place to the one after it, so the keys go on down the list.
function keepPicked(before) {
  for (const each of TABS) {
    const now = each.rows(view.value);
    const key = picked[each.id];
    if (now.some((row) => row[each.key] === key)) continue;
    const at = before ? Math.max(0, each.rows(before).findIndex((row) => row[each.key] === key)) : 0;
    picked[each.id] = now.length > 0 ? now[Math.min(at, now.length - 1)][each.key] : "";
  }
}

async function onState(next) {
  const before = view.value;
  view.value = next;
  keepPicked(before);
  count.value = Math.min(count.value, most.value);
  await nextTick();
  if (!before) {
    tab.value = "catch";
    answer.value = null;
    busy.value = false;
    await nextTick();
    focusMain();
    return;
  }

  const focused = document.activeElement;
  if (!focused || focused === document.body || focused.disabled || !focused.isConnected) focusMain();
}

function onAnswer(text, done) {
  busy.value = false;
  answer.value = { text, done };
}

function close() {
  view.value = null;
  answer.value = null;
  busy.value = false;
}

function leave() {
  if (view.value) props.bridge.close();
}

// A deal the fisher answers: the window waits for the answer and sends no second one meanwhile.
function request(send) {
  if (busy.value) return;
  busy.value = true;
  answer.value = null;
  send();
}

const sellOne = (row) => request(() => props.bridge.sell(row.item, 1));
// One deal sells no more than the fisher takes of a kind at once.
const sellingAll = (row) => Math.min(row.count, view.value.most);
const sellAllOf = (row) => request(() => props.bridge.sell(row.item, sellingAll(row)));
const describeSellAll = (row) =>
  fill(t.value[row.count > view.value.most ? "sell_many" : "sell_all_of"], { count: showNumber(sellingAll(row)), gold: showGold(row.price * sellingAll(row)) });
const sellEverything = () => request(() => props.bridge.sellAll());
const canBuy = computed(() => tab.value === "shop" && !!selected.value && !busy.value && total.value <= view.value.gold);
const buy = () => canBuy.value && request(() => props.bridge.buy(selected.value.ware, count.value));
const canRepair = (row) => !busy.value && row.price <= view.value.gold;
const repair = (row) => canRepair(row) && request(() => props.bridge.repair(row.item));

// Enter on the list: the tab's first deal for the row picked.
function act() {
  const row = selected.value;
  if (!row) return;
  if (tab.value === "catch") sellOne(row);
  else if (tab.value === "shop") buy();
  else repair(row);
}

function changeCount(by) {
  count.value = Math.min(most.value, Math.max(1, count.value + by));
}

function turnTab(by) {
  const at = TABS.findIndex((each) => each.id === tab.value);
  tab.value = TABS[(at + by + TABS.length) % TABS.length].id;
}

watch(tab, async () => {
  count.value = 1;
  answer.value = null;
  if (!selected.value && rows.value.length > 0) select(rows.value[0]);
  await nextTick();
  if (!tabList.value?.$el.contains(document.activeElement)) focusMain();
});

function moveTo(index) {
  const row = rows.value[Math.min(rows.value.length - 1, Math.max(0, index))];
  select(row);
  const at = rows.value.indexOf(row);
  nextTick(() => document.getElementById(optionId(at))?.scrollIntoView({ block: "nearest" }));
}

function onKeydown(event) {
  if (!view.value || isComposing(event) || event.defaultPrevented) return;
  if (isHeldPress(event)) {
    event.preventDefault();
    return;
  }

  const code = readCode(event);
  const key = event.key || code;
  const at = rows.value.findIndex((row) => row === selected.value);
  const moves = { ArrowDown: at + 1, ArrowUp: at - 1, Home: 0, End: rows.value.length - 1 };
  if (code in TAB_KEYS) {
    event.preventDefault();
    turnTab(TAB_KEYS[code]);
  } else if (key === "Escape") {
    event.preventDefault();
    leave();
  } else if (key in moves && rows.value.length > 0) {
    event.preventDefault();
    moveTo(moves[key]);
  } else if (counted.value && (key === "+" || key === "ArrowRight")) {
    event.preventDefault();
    changeCount(1);
  } else if (counted.value && (key === "-" || key === "ArrowLeft")) {
    event.preventDefault();
    changeCount(-1);
  } else if (key === "Enter" && event.target === list.value) {
    event.preventDefault();
    act();
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
  <div v-if="view" class="fisher">
    <RpPanel class="fisher__panel" variant="board" :title="t.title">
      <p class="fisher__purse">
        <RpIcon name="coins" class="fisher__coin" />
        <span>{{ t.purse }}</span>
        <strong data-purse>{{ showGold(view.gold) }}</strong>
      </p>

      <RpTabs ref="tabList" v-model="tab" :tabs="tabs" :label="t.tabs" :controls="panelId" />

      <div :id="panelId" class="fisher__content" role="tabpanel" :aria-labelledby="makeTabId(panelId, tab)" :data-tab="tab">
        <div v-if="rows.length === 0" class="fisher__empty" data-empty>
          <img class="fisher__picture fisher__picture--large fisher__picture--empty" :src="findPicture(EMPTY_PICTURES[tab])" alt="" />
          <p>{{ t[`empty.${tab}`] }}</p>
        </div>

        <div v-else class="fisher__body">
          <div class="fisher__side">
            <ul
              :id="listId"
              ref="list"
              class="fisher__rows"
              role="listbox"
              tabindex="0"
              :aria-label="t[`list.${tab}`]"
              :aria-activedescendant="selected ? optionId(rows.indexOf(selected)) : undefined"
              :aria-describedby="statusId"
            >
              <li
                v-for="(row, index) in rows"
                :id="optionId(index)"
                :key="keyOf(row)"
                :data-row="keyOf(row)"
                role="option"
                class="fisher__row"
                :class="{ 'fisher__row--on': row === selected, 'fisher__row--dear': isDear(row) }"
                :aria-selected="row === selected"
                @click="select(row)"
                @dblclick="act"
              >
                <img class="fisher__picture" :src="pictureOf(row)" alt="" />
                <span class="fisher__name">
                  <span>{{ row.name }}</span>
                  <span v-if="tab === 'catch' && row.trophy" class="fisher__tag">{{ t.trophy }}</span>
                  <span v-if="tab === 'repair'" class="fisher__tag fisher__tag--worn" :class="`fisher__tag--${row.condition.toLowerCase()}`">{{ t[`condition.${row.condition}`] }}</span>
                </span>
                <span v-if="tab === 'catch'" class="fisher__count">{{ fill(t.carried, { count: showNumber(row.count) }) }}</span>
                <span class="fisher__price">{{ showGold(row.price) }}</span>
              </li>
            </ul>

            <RpButton
              v-if="tab === 'catch'"
              class="fisher__everything"
              variant="primary"
              :disabled="busy"
              :aria-busy="busy"
              data-action="sell-everything"
              @click="sellEverything"
            >
              {{ fill(t.sell_everything, { gold: showGold(catchWorth) }) }}
            </RpButton>
          </div>

          <section v-if="selected" class="fisher__detail" :aria-label="selected.name">
            <img class="fisher__picture fisher__picture--large" :src="pictureOf(selected)" alt="" />
            <h2 class="fisher__detail-name">{{ selected.name }}</h2>

            <template v-if="tab === 'catch'">
              <p v-if="selected.trophy" class="fisher__tag fisher__tag--large">{{ t.trophy }}</p>
              <p class="fisher__facts">{{ fill(t.carry_count, { count: showNumber(selected.count) }) }}</p>
              <p class="fisher__each">{{ fill(t.each, { count: showNumber(selected.price) }) }}</p>
              <div class="fisher__acts">
                <RpButton variant="primary" :disabled="busy" :aria-busy="busy" data-action="sell-one" @click="sellOne(selected)">{{ t.sell_one }}</RpButton>
                <RpButton v-if="selected.count > 1" :disabled="busy" :aria-busy="busy" data-action="sell-all-of" @click="sellAllOf(selected)">
                  {{ describeSellAll(selected) }}
                </RpButton>
              </div>
            </template>

            <template v-else-if="tab === 'shop'">
              <p class="fisher__facts">{{ t[`kind.${selected.kind}`] }} · {{ fill(t.weight, { weight: showWeight(selected) }) }}</p>
              <p v-if="selected.count != null" class="fisher__facts" data-carried>{{ fill(t.carry_count, { count: showNumber(selected.count) }) }}</p>
              <p v-if="selected.reachCm != null" class="fisher__facts" data-rod-facts>
                {{ fill(t.reach, { metres: showMetres(selected.reachCm) }) }} · {{ fill(t.line, { percent: Math.round(selected.line * PERCENT) }) }}
              </p>
              <p class="fisher__each">{{ fill(t.each, { count: showNumber(selected.price) }) }}</p>
              <div v-if="counted" class="fisher__count-pick" role="group" :aria-label="t.count">
                <RpButton class="fisher__step" :aria-label="t.fewer" :disabled="count <= 1" @click="changeCount(-1)">−</RpButton>
                <output class="fisher__amount" aria-live="polite">{{ count }}</output>
                <RpButton class="fisher__step" :aria-label="t.more" :disabled="count >= most" @click="changeCount(1)">+</RpButton>
              </div>
              <p class="fisher__total" :class="{ 'fisher__total--dear': total > view.gold }">{{ fill(t.total, { count: showNumber(total) }) }}</p>
              <RpButton class="fisher__act" variant="primary" :disabled="!canBuy" :aria-busy="busy" data-action="buy" @click="buy">
                {{ total > view.gold ? t.too_poor : t.buy }}
              </RpButton>
            </template>

            <template v-else>
              <p class="fisher__facts">{{ t[`condition.${selected.condition}`] }}</p>
              <p class="fisher__each" :class="{ 'fisher__total--dear': isDear(selected) }">{{ fill(t.repair_note, { gold: showGold(selected.price) }) }}</p>
              <RpButton class="fisher__act" variant="primary" :disabled="!canRepair(selected)" :aria-busy="busy" data-action="repair" @click="repair(selected)">
                {{ isDear(selected) ? t.too_poor : t.repair }}
              </RpButton>
            </template>
          </section>
        </div>
      </div>

      <p :id="statusId" class="fisher__status" :class="answer && !busy && (answer.done ? 'fisher__status--done' : 'fisher__status--refused')" role="status">
        {{ status }}
      </p>

      <div class="fisher__footer">
        <RpButton data-action="close" @click="leave">{{ t.close }}</RpButton>
      </div>
    </RpPanel>
  </div>
</template>
