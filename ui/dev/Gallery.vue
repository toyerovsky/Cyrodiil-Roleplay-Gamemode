<script setup>
import { ref } from "vue";
import { TABS as ADMIN_TABS } from "../admin/tabs.js";
import { TABS as MENU_TABS } from "../menu/tabs.js";
import { PAGES } from "../pages.js";
import { RpButton, RpChip, RpField, RpIlluminated, RpPanel, RpRadial, RpTabs } from "../rp/index.js";

const name = ref("Marius");
const taken = ref("Varro");
const highlight = ref("closed");
const kind = ref("food");
const menuTab = ref("account");
const adminTab = ref("players");

const KINDS = ["all", "food", "tools", "weapons"];
const label = (each) => ({ ...each, label: each.id.charAt(0).toUpperCase() + each.id.slice(1) });
const menuTabs = MENU_TABS.map(label);
const adminTabs = ADMIN_TABS.map(label);

const wheelItems = [
  { id: "dice", label: "Roll a die", icon: "dice", disabled: false, reason: "" },
  { id: "letter", label: "Private message", icon: "letter", disabled: false, reason: "" },
  { id: "closed", label: "Trade", icon: "scales", disabled: true, reason: "Lucia Gallus is busy with another trade." },
  { id: "hand", label: "Greet", icon: "hand", disabled: false, reason: "" },
];

const buildUrl = (page, scenario) => `${page}/index.html${scenario ? `#${scenario}` : ""}`;
</script>

<template>
  <main class="gallery">
    <h1>Cyrodiil RP pages</h1>
    <p>The browser preview plays the game. Reload a page after changing its hash.</p>

    <section aria-labelledby="pages-title">
      <h2 id="pages-title">Pages</h2>
      <ul class="gallery__pages">
        <li v-for="page in PAGES" :key="page.name">
          <a :href="buildUrl(page.name, '')">{{ page.title }}</a>
          <nav :aria-label="`Scenarios: ${page.title}`">
            <a v-for="scenario in page.scenarios.filter(Boolean)" :key="scenario" :href="buildUrl(page.name, scenario)">{{ scenario }}</a>
          </nav>
        </li>
      </ul>
    </section>

    <section aria-labelledby="parts-title">
      <h2 id="parts-title">Design system</h2>
      <div class="gallery__parts">
        <RpPanel title="Panel">
          <RpField v-model="name" label="Field" placeholder="e.g. Marius" />
          <RpField v-model="taken" label="Refused field" invalid />
          <div class="gallery__buttons">
            <RpButton>Default</RpButton>
            <RpButton variant="primary">Primary</RpButton>
            <RpButton variant="primary" disabled>Disabled</RpButton>
          </div>
        </RpPanel>
        <RpPanel title="Letter" variant="letter">
          <RpIlluminated text="Written on the game's own parchment, in its gold frame, with an illuminated initial from its books." />
          <RpField v-model="name" label="Field on a letter" placeholder="e.g. Marius" />
          <RpField v-model="taken" label="Refused field on a letter" invalid />
          <RpButton variant="primary">Primary on a letter</RpButton>
        </RpPanel>
        <div class="gallery__wheel">
          <RpRadial v-model="highlight" :items="wheelItems" title="Lucia Gallus" label="Wheel" />
        </div>
        <RpPanel title="Chips and tabs">
          <div class="gallery__chips" role="group" aria-label="Kinds">
            <RpChip v-for="each in KINDS" :key="each" :pressed="kind === each" @click="kind = each">{{ each }}</RpChip>
          </div>
          <div class="gallery__chips" role="group" aria-label="Weather">
            <RpChip>Rain</RpChip>
            <RpChip disabled>Snow</RpChip>
          </div>
          <div class="gallery__tabs">
            <RpTabs v-model="menuTab" :tabs="menuTabs" label="Large tabs" controls="gallery-large" />
            <RpTabs v-model="adminTab" :tabs="adminTabs" label="Small tabs" controls="gallery-small" size="small" />
          </div>
        </RpPanel>
      </div>
    </section>
  </main>
</template>
