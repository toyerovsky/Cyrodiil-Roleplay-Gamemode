<script setup>
import { computed, nextTick, onMounted, onUnmounted, ref, useId, useTemplateRef } from "vue";
import crest from "../rp/art/crest.webp";
import { makeTabId, RpButton, RpIlluminated, RpPanel, RpTabs } from "../rp/index.js";
import { usePageTexts } from "../shared/locale.js";
import { isComposing, isHeldPress, readCode } from "../shared/press.js";
import AccountTab from "./AccountTab.vue";
import CharacterTab from "./CharacterTab.vue";
import DiceTab from "./DiceTab.vue";
import FactionTab from "./FactionTab.vue";
import HelpTab from "./HelpTab.vue";
import KeysTab from "./KeysTab.vue";
import LoreTab from "./LoreTab.vue";
import { TABS } from "./tabs.js";

const props = defineProps({ bridge: { type: Object, required: true } });

// PageUp and PageDown move the tab's text by this much of what shows of it.
const PAGE_STEP = 0.8;
const PAGE = { PageUp: -1, PageDown: 1 };
// F10 is not the page's: the game's own key closes the menu (PlayerMenuOverlay), and closing here too would have it
// open the menu again a moment later.
const CLOSING = "Escape";

const { locale, t } = usePageTexts(props.bridge, "menu");

const menu = ref(null);
const tab = ref(TABS[0].id);
// What the faction tab's last command came to, and whether one is still on its way.
const answer = ref(null);
const waiting = ref(false);
const content = useTemplateRef("content");
const tabList = useTemplateRef("tabList");
const panelId = `${useId()}-panel`;

const name = computed(() => (menu.value ? `${menu.value.character.firstName} ${menu.value.character.lastName}` : ""));
const tabs = computed(() => TABS.map((each) => ({ ...each, label: t.value[`tab.${each.id}`] })));

function select(id) {
  if (id === tab.value) return;
  tab.value = id;
  forgetAnswer();
  if (content.value) content.value.scrollTop = 0;
}

async function onState(view) {
  const opening = !menu.value;
  const moved = view.tab !== tab.value;
  menu.value = view;
  tab.value = view.tab;
  if (!opening && !moved) return;
  await nextTick();
  tabList.value.focus();
}

function forgetAnswer() {
  answer.value = null;
  waiting.value = false;
}

function runFaction(line) {
  answer.value = null;
  waiting.value = true;
  props.bridge.runFaction(line);
}

function onAnswer(text, done) {
  waiting.value = false;
  answer.value = { text, done };
}

function pickLanguage(code) {
  if (code !== menu.value.language) props.bridge.setLanguage(code);
}

function pickDiceSet(set) {
  if (set !== menu.value.dice.set) props.bridge.chooseDiceSet(set);
}

function pickNameVisibility(isPublic) {
  if (isPublic !== menu.value.lore.namePublic) props.bridge.setNamePublic(isPublic);
}

function leave() {
  if (menu.value) props.bridge.close();
}

function onKeydown(event) {
  if (!menu.value || isComposing(event)) return;
  if (isHeldPress(event)) {
    event.preventDefault();
    return;
  }

  // Q, E and the arrows typed into a field are its own, and only Escape leaves it.
  const code = readCode(event);
  if (event.target?.tagName === "INPUT") {
    if (code === CLOSING) {
      event.preventDefault();
      leave();
    }
    return;
  }

  // The tabs turn from anywhere else in the menu. A key the tab list took itself, holding the focus, turned them
  // already.
  if (event.defaultPrevented || tabList.value.takeKey(event)) return;

  if (code in PAGE && content.value) {
    event.preventDefault();
    content.value.scrollBy({ top: PAGE[code] * content.value.clientHeight * PAGE_STEP });
  } else if (code === CLOSING) {
    event.preventDefault();
    leave();
  }
}

onMounted(() => {
  props.bridge.onState(onState);
  props.bridge.onClosed(() => {
    menu.value = null;
    forgetAnswer();
  });
  props.bridge.onAnswer(onAnswer);
  window.addEventListener("keydown", onKeydown);
  props.bridge.signalReady();
});

onUnmounted(() => window.removeEventListener("keydown", onKeydown));
</script>

<template>
  <div v-if="menu" class="menu">
    <div class="menu__document">
      <img class="menu__crest" :src="crest" alt="">
      <RpPanel class="menu__panel" variant="letter" :title="name">
        <template #title>
          <RpIlluminated class="menu__name" tag="span" :text="name" />
        </template>

        <div class="menu__body">
          <RpTabs ref="tabList" :model-value="tab" :tabs="tabs" :label="t.tabs" :controls="panelId" @update:model-value="select" />

          <div :id="panelId" ref="content" class="menu__content" role="tabpanel" :aria-labelledby="makeTabId(panelId, tab)" tabindex="0">
            <CharacterTab v-if="tab === 'character'" :name="name" :character="menu.character" :t="t" :locale="locale" />
            <AccountTab
              v-else-if="tab === 'account'"
              :account="menu.account"
              :languages="menu.languages"
              :language="menu.language"
              :t="t"
              :locale="locale"
              @pick="pickLanguage"
            />
            <HelpTab v-else-if="tab === 'help'" :help="menu.help" :t="t" />
            <DiceTab v-else-if="tab === 'dice'" :dice="menu.dice" :t="t" :locale="locale" @open="bridge.openDice" @pick="pickDiceSet" />
            <LoreTab v-else-if="tab === 'lore'" :lore="menu.lore" :t="t" :locale="locale" @open="bridge.openLore" @name="pickNameVisibility" />
            <FactionTab
              v-else-if="tab === 'faction'"
              :faction="menu.faction"
              :answer="answer"
              :waiting="waiting"
              :t="t"
              :locale="locale"
              @run="runFaction"
            />
            <KeysTab v-else :t="t" />
          </div>
        </div>

        <div class="menu__footer">
          <p class="menu__hint">{{ t.hint }}</p>
          <RpButton class="menu__close" @click="leave">{{ t.close }}</RpButton>
        </div>
      </RpPanel>
    </div>
  </div>
</template>
