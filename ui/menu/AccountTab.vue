<script setup>
import { RpChip } from "../rp/index.js";
import { formatDate, formatMoment } from "../shared/format.js";
import { fill } from "../shared/locale.js";

defineProps({
  account: { type: Object, required: true },
  languages: { type: Array, required: true },
  language: { type: String, required: true },
  t: { type: Object, required: true },
  locale: { type: String, required: true },
});

const emit = defineEmits(["pick"]);

// Each language in its own words.
function nameLanguage(code) {
  const name = new Intl.DisplayNames([code], { type: "language" }).of(code) ?? code;
  return name.charAt(0).toLocaleUpperCase(code) + name.slice(1);
}
</script>

<template>
  <dl class="menu__facts">
    <div v-if="account.nickname" class="menu__fact">
      <dt>{{ t["account.nickname"] }}</dt>
      <dd>{{ account.nickname }}</dd>
    </div>
    <div class="menu__fact">
      <dt>{{ t["account.created"] }}</dt>
      <dd>{{ formatDate(account.createdAt, locale) }}</dd>
    </div>
    <div class="menu__fact">
      <dt>{{ t["account.previous"] }}</dt>
      <dd>{{ account.previousVisitAt === null ? t["account.first_visit"] : formatMoment(account.previousVisitAt, locale) }}</dd>
    </div>
  </dl>

  <h2 class="menu__heading">{{ t["account.sanctions"] }}</h2>
  <ul v-if="account.sanctions.length" class="menu__sanctions">
    <li v-for="(sanction, index) in account.sanctions" :key="index" class="menu__sanction">
      <strong class="menu__sanction-title">{{ sanction.title }}</strong>
      <span v-if="sanction.reason">{{ fill(t["account.reason"], { reason: sanction.reason }) }}</span>
      <span class="menu__sanction-end">
        {{ sanction.endsAt === null ? t["account.until_lifted"] : fill(t["account.ends"], { date: formatMoment(sanction.endsAt, locale) }) }}
      </span>
    </li>
  </ul>
  <p v-else class="menu__note">{{ t["account.no_sanctions"] }}</p>

  <!-- Toggle buttons, not radios: the menu's arrows turn its tabs, and a language picked rewrites the whole menu. -->
  <template v-if="languages.length > 1">
    <h2 class="menu__heading">{{ t["account.language"] }}</h2>
    <div class="menu__languages" role="group" :aria-label="t['account.language']">
      <RpChip v-for="code in languages" :key="code" :pressed="code === language" :lang="code" @click="emit('pick', code)">
        {{ nameLanguage(code) }}
      </RpChip>
    </div>
  </template>
</template>
