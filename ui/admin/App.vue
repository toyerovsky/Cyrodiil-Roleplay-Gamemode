<script setup>
import { computed, nextTick, onMounted, onUnmounted, ref, useId, useTemplateRef } from "vue";
import { makeTabId, RpButton, RpPanel, RpTabs } from "../rp/index.js";
import { usePageTexts } from "../shared/locale.js";
import { isComposing, isHeldPress } from "../shared/press.js";
import { ACTIONS, buildLine } from "./actions.js";
import ActionDialog from "./ActionDialog.vue";
import AuditTab from "./AuditTab.vue";
import DiceTab from "./DiceTab.vue";
import { RECENT_KEPT } from "./items.js";
import ItemsTab from "./ItemsTab.vue";
import PlayersTab from "./PlayersTab.vue";
import SanctionsTab from "./SanctionsTab.vue";
import { PARTS, TABS } from "./tabs.js";
import WorldTab from "./WorldTab.vue";

const props = defineProps({ bridge: { type: Object, required: true } });

const { locale, t } = usePageTexts(props.bridge, "admin");

const view = ref(null);
const tab = ref(TABS[0].id);
const selected = ref(null);
const card = ref(null);
const sanctions = ref(null);
const audit = ref(null);
const rolls = ref(null);
const found = ref([]);
const answer = ref(null);
const waiting = ref(false);
const dialog = ref(null);
// The items given last: kept while the game runs, closed panel or not.
const recent = ref([]);
const tabList = useTemplateRef("tabList");
const panelId = `${useId()}-panel`;
let auditFilter = {};
let rollFilter = {};

const tabs = computed(() =>
  TABS.filter((each) => view.value?.tabs.includes(each.id)).map((each) => ({ ...each, label: t.value[`tab.${each.id}`] })),
);
const duty = computed(() => {
  if (view.value.onDuty) return "on";
  return view.value.acting ? "free" : "off";
});

// May the roles run it at all, and may the staff member run it now: only /duty itself goes without the duty.
const may = (action) => view.value?.allowed.includes(ACTIONS[action].command) ?? false;
const can = (action) => may(action) && (view.value.acting || action === "duty");

function run(line) {
  waiting.value = true;
  answer.value = null;
  props.bridge.run(line);
}

// A button whose command needs more asks for it first.
function act(action, values, name) {
  if (!can(action)) return;
  if (ACTIONS[action].fields) {
    dialog.value = { action, values, name };
    return;
  }

  if (values.entry) remember(values.entry);
  run(buildLine(action, values));
}

function remember(entry) {
  recent.value = [entry, ...recent.value.filter((each) => each.id !== entry.id)].slice(0, RECENT_KEPT);
}

// Null clears what was found: the text got too short to look for.
function searchItems(filter) {
  if (filter) props.bridge.ask(PARTS.items, JSON.stringify(filter));
  else found.value = [];
}

function confirm(values) {
  const { action } = dialog.value;
  dialog.value = null;
  run(buildLine(action, values));
}

function select(number) {
  selected.value = number;
  card.value = null;
  found.value = [];
  props.bridge.ask(PARTS.card, String(number));
}

function searchAudit(filter, before = null) {
  if (!before) auditFilter = filter;
  props.bridge.ask(PARTS.audit, JSON.stringify({ ...auditFilter, before }));
}

function searchRolls(filter, before = null) {
  if (!before) rollFilter = filter;
  props.bridge.ask(PARTS.rolls, JSON.stringify({ ...rollFilter, before }));
}

// A tab shows what the server holds as it is now.
function load(id) {
  if (id === "sanctions") props.bridge.ask(PARTS.sanctions, "");
  if (id === "audit") searchAudit(auditFilter);
  if (id === "dice") searchRolls(rollFilter);
}

function pick(id) {
  if (id === tab.value) return;
  tab.value = id;
  found.value = [];
  load(id);
}

async function onState(next) {
  const opening = !view.value;
  view.value = next;
  if (opening) {
    tab.value = next.tab;
    load(next.tab);
  }
  if (next.focus != null) {
    tab.value = "players";
    select(next.focus);
  }
  if (!opening) return;
  await nextTick();
  tabList.value.focus();
}

function onPart(kind, data) {
  if (kind === PARTS.card && data.number === selected.value) card.value = data;
  else if (kind === PARTS.sanctions) sanctions.value = data;
  else if (kind === PARTS.items) found.value = data;
  else if (kind === PARTS.audit) audit.value = data.appended && audit.value ? { ...data, entries: [...audit.value.entries, ...data.entries] } : data;
  else if (kind === PARTS.rolls) rolls.value = data.appended && rolls.value ? { ...data, rolls: [...rolls.value.rolls, ...data.rolls] } : data;
}

// What a command changed shows again: the card open, the sanctions in force and the audit, which it wrote to.
function onAnswer(text, done) {
  waiting.value = false;
  answer.value = { text, done };
  if (selected.value !== null) props.bridge.ask(PARTS.card, String(selected.value));
  if (tab.value === "sanctions") props.bridge.ask(PARTS.sanctions, "");
  if (tab.value === "audit") searchAudit(auditFilter);
}

function reset() {
  view.value = null;
  selected.value = null;
  card.value = null;
  sanctions.value = null;
  audit.value = null;
  rolls.value = null;
  found.value = [];
  answer.value = null;
  waiting.value = false;
  dialog.value = null;
  auditFilter = {};
  rollFilter = {};
}

function leave() {
  if (view.value) props.bridge.close();
}

function onKeydown(event) {
  if (!view.value || isComposing(event)) return;
  if (isHeldPress(event)) {
    event.preventDefault();
    return;
  }

  if (event.key === "Escape") {
    event.preventDefault();
    if (dialog.value) dialog.value = null;
    else leave();
  }
}

onMounted(() => {
  props.bridge.onState(onState);
  props.bridge.onPart(onPart);
  props.bridge.onAnswer(onAnswer);
  props.bridge.onClosed(reset);
  window.addEventListener("keydown", onKeydown);
  props.bridge.signalReady();
});

onUnmounted(() => window.removeEventListener("keydown", onKeydown));
</script>

<template>
  <div v-if="view" class="admin">
    <RpPanel class="admin__panel" :title="t.title">
      <div class="admin__duty" :class="`admin__duty--${duty}`">
        <span class="admin__duty-state" role="status">{{ t[`duty.${duty}`] }}</span>
        <RpButton v-if="may('duty')" :variant="view.onDuty ? 'default' : 'primary'" @click="act('duty', {})">
          {{ view.onDuty ? t["duty.end"] : t["duty.start"] }}
        </RpButton>
      </div>
      <p v-if="!view.acting" class="admin__needed">{{ t["duty.needed"] }}</p>

      <div class="admin__body">
        <!-- The tabs turn only from the tab list: Q and E elsewhere are letters typed into the panel's fields. -->
        <RpTabs ref="tabList" :model-value="tab" :tabs="tabs" :label="t.tabs" :controls="panelId" size="small" @update:model-value="pick" />

        <div :id="panelId" class="admin__content" role="tabpanel" :aria-labelledby="makeTabId(panelId, tab)">
          <PlayersTab
            v-if="tab === 'players'"
            :players="view.players"
            :selected="selected"
            :card="card"
            :found="found"
            :t="t"
            :locale="locale"
            :may="may"
            :can="can"
            @select="select"
            @act="act"
            @search="searchItems"
          />
          <ItemsTab
            v-else-if="tab === 'items'"
            :view="view"
            :found="found"
            :recent="recent"
            :t="t"
            :can="can"
            @search="searchItems"
            @act="act"
          />
          <SanctionsTab v-else-if="tab === 'sanctions'" :sanctions="sanctions" :t="t" :locale="locale" :may="may" :can="can" @act="act" />
          <AuditTab v-else-if="tab === 'audit'" :audit="audit" :t="t" :locale="locale" @search="searchAudit" />
          <DiceTab v-else-if="tab === 'dice'" :rolls="rolls" :t="t" :locale="locale" @search="searchRolls" />
          <WorldTab v-else :view="view" :t="t" :may="may" :can="can" @act="act" />
        </div>
      </div>

      <div class="admin__footer">
        <p class="admin__status" :class="answer && (answer.done ? 'admin__status--done' : 'admin__status--refused')" role="status">
          {{ waiting ? t.waiting : answer?.text ?? t.hint }}
        </p>
        <RpButton @click="leave">{{ t.close }}</RpButton>
      </div>
    </RpPanel>

    <ActionDialog v-if="dialog" :dialog="dialog" :t="t" @run="confirm" @cancel="dialog = null" />
  </div>
</template>
