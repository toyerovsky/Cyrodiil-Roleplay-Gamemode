<script setup>
import { computed, ref } from "vue";
import { RpButton, RpField } from "../rp/index.js";
import { fill } from "../shared/locale.js";
import { formatDate } from "./format.js";

// Every faction beside the one picked: its ranks, its roll, and what the staff may do to it. The list and the card are
// read from the server when the tab opens, when a faction is picked and after every command.
const props = defineProps({
  factions: { type: Array, default: null },
  selected: { type: Number, default: null },
  card: { type: Object, default: null },
  t: { type: Object, required: true },
  locale: { type: String, required: true },
  may: { type: Function, required: true },
  can: { type: Function, required: true },
});

const emit = defineEmits(["select", "act"]);

const MANAGING = ["factionRename", "factionDissolve"];

const query = ref("");

const shown = computed(() => {
  const text = query.value.trim().toLocaleLowerCase(props.locale);
  return props.factions.filter(
    (faction) => text === "" || `#${faction.id}` === text || String(faction.id) === text || faction.name.toLocaleLowerCase(props.locale).includes(text),
  );
});

const findRank = (number) => props.card.ranks.find((rank) => rank.number === number);

function describeRank(rank) {
  if (rank.kind === "Leader") return props.t["factions.every_permission"];
  if (!rank.permissions.length) return props.t["factions.no_permissions"];
  return rank.permissions.map((permission) => props.t[`factions.perm.${permission}`]).join(", ");
}

const describeSeen = (member) =>
  member.online === null
    ? fill(props.t["factions.last_seen"], { date: formatDate(member.lastSeenAt, props.locale) })
    : fill(props.t["factions.online"], { number: member.online });

const act = (action, values, name) => emit("act", action, { id: props.card.id, ...values }, name);
</script>

<template>
  <p v-if="!factions" class="admin__note" role="status">{{ t.loading }}</p>
  <div v-else class="admin-factions">
    <section class="admin-factions__list" :aria-label="t['factions.list']">
      <RpField v-model="query" :label="t['factions.search']" type="search" />
      <ul v-if="shown.length" class="admin-factions__rows">
        <li v-for="faction in shown" :key="faction.id">
          <button
            type="button"
            class="admin-faction"
            :class="{ 'admin-faction--on': faction.id === selected }"
            :aria-pressed="faction.id === selected"
            :data-faction="faction.id"
            @click="emit('select', faction.id)"
          >
            <span class="admin-faction__number">#{{ faction.id }}</span>
            <span class="admin-faction__name">{{ faction.name }}</span>
            <span class="admin-faction__meta">{{ fill(t["factions.count"], { members: faction.members, online: faction.online }) }}</span>
            <span class="admin-faction__meta">
              {{ faction.leaders.length ? fill(t["factions.leaders"], { names: faction.leaders.join(", ") }) : t["factions.no_leader"] }}
            </span>
          </button>
        </li>
      </ul>
      <p v-else class="admin__note">{{ factions.length ? t["factions.none_found"] : t["factions.none"] }}</p>
    </section>

    <p v-if="selected === null" class="admin__note">{{ t["factions.pick"] }}</p>
    <p v-else-if="!card" class="admin__note" role="status">{{ t.loading }}</p>
    <section v-else class="admin-factions__card" :aria-label="card.name" :data-card="card.id">
      <h3 class="admin-factions__title">{{ card.name }}<span class="admin-factions__id">#{{ card.id }}</span></h3>
      <p class="admin-factions__founded">{{ fill(t["factions.founded"], { date: formatDate(card.foundedAt, locale) }) }}</p>
      <div v-if="MANAGING.some(may)" class="admin-chips">
        <template v-for="action in MANAGING" :key="action">
          <RpButton v-if="may(action)" :disabled="!can(action)" :data-action="action" @click="act(action, {}, card.name)">
            {{ t[`action.${action}`] }}
          </RpButton>
        </template>
      </div>

      <h4 class="admin__heading">{{ t["factions.ranks"] }}</h4>
      <ol class="admin-factions__ranks">
        <li v-for="rank in card.ranks" :key="rank.number" class="admin-factions__rank" :data-rank="rank.number">
          <strong class="admin-factions__rank-name">{{ rank.name }}</strong>
          <span>{{ describeRank(rank) }}</span>
          <span class="admin-factions__holders">{{ fill(t["factions.holders"], { count: rank.members }) }}</span>
        </li>
      </ol>

      <h4 class="admin__heading">{{ t["factions.members"] }}</h4>
      <table class="admin-table admin-factions__roll">
        <thead>
          <tr>
            <th scope="col">{{ t["factions.col.name"] }}</th>
            <th scope="col">{{ t["factions.col.rank"] }}</th>
            <th scope="col">{{ t["factions.col.seen"] }}</th>
            <th scope="col"><span class="admin__hidden">{{ t["factions.col.actions"] }}</span></th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="member in card.members" :key="member.id" :data-member="member.id">
            <td class="admin-factions__member">{{ member.name }}</td>
            <td>{{ findRank(member.rank).name }}</td>
            <td :class="{ 'admin-factions__online': member.online !== null }">{{ describeSeen(member) }}</td>
            <td>
              <span v-if="may('factionLead')" class="admin-factions__acts">
                <RpButton
                  v-if="!member.leader"
                  :disabled="!can('factionLead')"
                  data-action="factionLead"
                  @click="act('factionLead', { member: member.id }, member.name)"
                >
                  {{ t["action.factionLead"] }}
                </RpButton>
                <RpButton :disabled="!can('factionRemove')" data-action="factionRemove" @click="act('factionRemove', { member: member.id }, member.name)">
                  {{ t["action.factionRemove"] }}
                </RpButton>
              </span>
            </td>
          </tr>
        </tbody>
      </table>
    </section>
  </div>
</template>
