<script setup>
import { computed, onUnmounted, ref, watch } from "vue";
import { RpButton, RpChip, RpField } from "../rp/index.js";
import { formatDate } from "../shared/format.js";
import { FACTION_NAME_MAX, FACTION_NAME_MIN, FACTION_RANK_NAME_MAX, FACTION_RANK_NAME_MIN } from "../shared/limits.js";
import { fill } from "../shared/locale.js";
import { PERMISSIONS } from "./faction.js";

// The player's faction (MenuView.faction): its roll and its ranks, or the way into one. Every button runs a /faction
// command on the server, which checks it again and answers with the menu as it is after it. What the reader may do
// comes from the server (the "can" of each member and rank), so nothing is offered that would be refused.
const props = defineProps({
  faction: { type: Object, required: true },
  answer: { type: Object, default: null },
  waiting: Boolean,
  t: { type: Object, required: true },
  locale: { type: String, required: true },
});

const emit = defineEmits(["run"]);

// A removal is asked twice, so a slip of the mouse removes nothing.
const CONFIRM_MS = 2500;

const name = ref("");
const rankName = ref("");
const renamed = ref("");
const problem = ref(null);
const ranking = ref(null);
const renaming = ref(null);
const confirming = ref(null);
let confirmTimer = 0;

const card = computed(() => props.faction.faction);
const invite = computed(() => props.faction.invite);
const present = computed(() => card.value.members.filter((member) => member.online).length);
const ownRank = computed(() => findRank(card.value.rank));

const clean = (text) => text.trim().replace(/\s+/g, " ");
const findRank = (number) => card.value.ranks.find((rank) => rank.number === number);
const listGivable = (member) => card.value.ranks.filter((rank) => rank.canGive && rank.number !== member.rank);
const nameAllowed = (rank) => rank.permissions.map((permission) => props.t[`faction.perm.${permission}`]).join(", ");

function run(line) {
  problem.value = null;
  emit("run", line);
}

// Null when the name is of a length the server takes, its refusal otherwise.
function checkLength(text, min, max, key) {
  return text.length >= min && text.length <= max ? null : fill(props.t[key], { min, max });
}

function found() {
  const wanted = clean(name.value);
  const refusal = checkLength(wanted, FACTION_NAME_MIN, FACTION_NAME_MAX, "faction.name_length");
  if (refusal) problem.value = { field: "name", text: refusal };
  else run(`/faction create ${wanted}`);
}

function addRank() {
  const wanted = clean(rankName.value);
  const refusal = checkLength(wanted, FACTION_RANK_NAME_MIN, FACTION_RANK_NAME_MAX, "faction.rank_name_length");
  if (refusal) problem.value = { field: "rank", text: refusal };
  else run(`/faction rankadd ${wanted}`);
}

function startRename(rank) {
  renaming.value = rank.number;
  renamed.value = rank.name;
  problem.value = null;
}

function saveRename(rank) {
  const wanted = clean(renamed.value);
  const refusal = checkLength(wanted, FACTION_RANK_NAME_MIN, FACTION_RANK_NAME_MAX, "faction.rank_name_length");
  if (refusal) problem.value = { field: "rename", text: refusal };
  else run(`/faction rankname #${rank.number} ${wanted}`);
}

function giveRank(member, rank) {
  ranking.value = null;
  run(`/faction rank #${member.id} #${rank.number}`);
}

function togglePermission(rank, permission) {
  const held = rank.permissions.includes(permission);
  run(`/faction perm #${rank.number} ${permission.toLowerCase()} ${held ? "off" : "on"}`);
}

// The first click arms the button for a moment, the second runs the line.
function confirm(key, line) {
  clearTimeout(confirmTimer);
  if (confirming.value === key) {
    confirming.value = null;
    run(line);
    return;
  }
  confirming.value = key;
  confirmTimer = setTimeout(() => (confirming.value = null), CONFIRM_MS);
}

// What was typed goes once the server took it, and stays to be put right when it refused.
watch(
  () => props.answer,
  (answer) => {
    if (!answer?.done) return;
    name.value = "";
    rankName.value = "";
    renaming.value = null;
  },
);

onUnmounted(() => clearTimeout(confirmTimer));
</script>

<template>
  <p
    v-if="waiting || answer"
    class="menu__faction-answer"
    :class="{ 'menu__faction-answer--refused': !waiting && !answer.done }"
    :role="!waiting && !answer.done ? 'alert' : 'status'"
  >
    {{ waiting ? t["faction.waiting"] : answer.text }}
  </p>

  <template v-if="!card">
    <p class="menu__note">{{ t["faction.lead"] }}</p>

    <section v-if="invite" class="menu__group" data-part="invite">
      <h2 class="menu__heading">{{ t["faction.invitation"] }}</h2>
      <p class="menu__faction-text">{{ fill(t["faction.invited_by"], { name: invite.by, faction: invite.faction }) }}</p>
      <div class="menu__faction-acts">
        <RpButton variant="primary" data-action="accept" @click="run('/faction accept')">{{ t["faction.join"] }}</RpButton>
        <RpButton data-action="decline" @click="run('/faction decline')">{{ t["faction.decline"] }}</RpButton>
      </div>
    </section>

    <section class="menu__group" data-part="found">
      <h2 class="menu__heading">{{ t["faction.found"] }}</h2>
      <p class="menu__faction-text">
        {{ faction.fee > 0 ? fill(t["faction.found_fee"], { fee: faction.fee.toLocaleString(locale) }) : t["faction.found_free"] }}
      </p>
      <form class="menu__faction-form" novalidate @submit.prevent="found">
        <RpField
          v-model="name"
          :label="t['faction.name']"
          :maxlength="FACTION_NAME_MAX"
          :invalid="problem?.field === 'name'"
          autocomplete="off"
          data-field="name"
        />
        <RpButton type="submit" variant="primary" data-action="found">{{ t["faction.found_button"] }}</RpButton>
      </form>
      <p v-if="problem?.field === 'name'" class="menu__faction-problem" role="alert">{{ problem.text }}</p>
    </section>
  </template>

  <template v-else>
    <dl class="menu__facts">
      <div class="menu__fact">
        <dt>{{ t["faction.name"] }}</dt>
        <dd>{{ card.name }}</dd>
      </div>
      <div class="menu__fact">
        <dt>{{ t["faction.founded"] }}</dt>
        <dd>{{ formatDate(card.foundedAt, locale) }}</dd>
      </div>
      <div class="menu__fact">
        <dt>{{ t["faction.your_rank"] }}</dt>
        <dd>{{ ownRank.name }}</dd>
      </div>
      <div class="menu__fact">
        <dt>{{ t["faction.members"] }}</dt>
        <dd>{{ fill(t["faction.members_count"], { count: card.members.length, online: present }) }}</dd>
      </div>
    </dl>

    <section class="menu__group" data-part="roll">
      <h2 class="menu__heading">{{ t["faction.roll"] }}</h2>
      <p v-if="card.canInvite" class="menu__note menu__faction-how">{{ t["faction.invite_how"] }}</p>
      <ul class="menu__faction-members">
        <li v-for="member in card.members" :key="member.id" class="menu__faction-member" :data-member="member.id">
          <span class="menu__faction-name">
            {{ member.name }}<span v-if="member.self" class="menu__faction-you">{{ t["faction.you"] }}</span>
          </span>
          <span class="menu__faction-rank">{{ findRank(member.rank).name }}</span>
          <span class="menu__faction-seen" :class="{ 'menu__faction-seen--online': member.online }">
            {{ member.online ? t["faction.online"] : fill(t["faction.last_seen"], { date: formatDate(member.lastSeenAt, locale) }) }}
          </span>
          <span class="menu__faction-acts">
            <RpButton
              v-if="member.canRank"
              data-action="rank"
              :aria-expanded="ranking === member.id"
              @click="ranking = ranking === member.id ? null : member.id"
            >
              {{ t["faction.change_rank"] }}
            </RpButton>
            <RpButton v-if="member.canKick" data-action="kick" @click="confirm(`kick:${member.id}`, `/faction kick #${member.id}`)">
              {{ confirming === `kick:${member.id}` ? t["faction.sure"] : t["faction.remove"] }}
            </RpButton>
          </span>
          <div
            v-if="ranking === member.id"
            class="menu__faction-choice"
            role="group"
            :aria-label="fill(t['faction.rank_for'], { name: member.name })"
          >
            <span class="menu__faction-choice-label">{{ fill(t["faction.rank_for"], { name: member.name }) }}</span>
            <RpChip v-for="rank in listGivable(member)" :key="rank.number" :data-give="rank.number" @click="giveRank(member, rank)">
              {{ rank.name }}
            </RpChip>
          </div>
        </li>
      </ul>
    </section>

    <section class="menu__group" data-part="ranks">
      <h2 class="menu__heading">{{ t["faction.ranks"] }}</h2>
      <ol class="menu__faction-ranks">
        <li v-for="rank in card.ranks" :key="rank.number" class="menu__faction-rank-row" :data-rank="rank.number">
          <form v-if="renaming === rank.number" class="menu__faction-form" novalidate @submit.prevent="saveRename(rank)">
            <RpField
              v-model="renamed"
              :label="t['faction.rank_name']"
              :maxlength="FACTION_RANK_NAME_MAX"
              :invalid="problem?.field === 'rename'"
              autocomplete="off"
              data-field="rename"
            />
            <RpButton type="submit" variant="primary" data-action="save">{{ t["faction.save"] }}</RpButton>
            <RpButton data-action="cancel" @click="renaming = null">{{ t["faction.cancel"] }}</RpButton>
          </form>
          <p v-else class="menu__faction-rank-head">
            <strong class="menu__faction-rank-name">{{ rank.name }}</strong>
            <span class="menu__faction-holders">{{ fill(t["faction.holders"], { count: rank.members }) }}</span>
          </p>
          <p v-if="renaming === rank.number && problem?.field === 'rename'" class="menu__faction-problem" role="alert">{{ problem.text }}</p>

          <p v-if="rank.kind === 'Leader'" class="menu__faction-allows">{{ t["faction.rank_leader"] }}</p>
          <div v-else-if="rank.canEdit" class="menu__faction-perms" role="group" :aria-label="fill(t['faction.perms_of'], { rank: rank.name })">
            <RpChip
              v-for="permission in PERMISSIONS"
              :key="permission"
              :pressed="rank.permissions.includes(permission)"
              :disabled="!card.grantable.includes(permission)"
              :data-perm="permission"
              @click="togglePermission(rank, permission)"
            >
              {{ t[`faction.perm.${permission}`] }}
            </RpChip>
          </div>
          <p v-else class="menu__faction-allows">
            {{ rank.permissions.length ? fill(t["faction.allows"], { list: nameAllowed(rank) }) : t["faction.allows_nothing"] }}
          </p>
          <p v-if="rank.kind === 'Entry'" class="menu__faction-allows menu__faction-allows--note">{{ t["faction.rank_entry"] }}</p>

          <div v-if="rank.canRename || rank.canMoveUp || rank.canMoveDown || rank.canDelete" class="menu__faction-acts">
            <RpButton v-if="rank.canRename && renaming !== rank.number" data-action="rename" @click="startRename(rank)">
              {{ t["faction.rename"] }}
            </RpButton>
            <RpButton v-if="rank.canMoveUp" data-action="up" @click="run(`/faction rankmove #${rank.number} up`)">{{ t["faction.up"] }}</RpButton>
            <RpButton v-if="rank.canMoveDown" data-action="down" @click="run(`/faction rankmove #${rank.number} down`)">
              {{ t["faction.down"] }}
            </RpButton>
            <RpButton v-if="rank.canDelete" data-action="delete" @click="confirm(`delete:${rank.number}`, `/faction rankdel #${rank.number}`)">
              {{ confirming === `delete:${rank.number}` ? t["faction.sure"] : t["faction.delete"] }}
            </RpButton>
          </div>
        </li>
      </ol>

      <template v-if="card.canAddRank">
        <form class="menu__faction-form" novalidate @submit.prevent="addRank">
          <RpField
            v-model="rankName"
            :label="t['faction.new_rank']"
            :maxlength="FACTION_RANK_NAME_MAX"
            :invalid="problem?.field === 'rank'"
            autocomplete="off"
            data-field="rank"
          />
          <RpButton type="submit" data-action="add">{{ t["faction.add_rank"] }}</RpButton>
        </form>
        <p v-if="problem?.field === 'rank'" class="menu__faction-problem" role="alert">{{ problem.text }}</p>
      </template>
    </section>

    <section class="menu__group" data-part="leave">
      <h2 class="menu__heading">{{ t["faction.leaving"] }}</h2>
      <p v-if="!card.canLeave" class="menu__note">{{ t["faction.sole_leader"] }}</p>
      <template v-else>
        <p v-if="card.members.length === 1" class="menu__note menu__faction-how">{{ t["faction.last_member"] }}</p>
        <RpButton data-action="leave" @click="confirm('leave', '/faction leave')">
          {{ confirming === "leave" ? t["faction.sure"] : t["faction.leave"] }}
        </RpButton>
      </template>
    </section>
  </template>
</template>
