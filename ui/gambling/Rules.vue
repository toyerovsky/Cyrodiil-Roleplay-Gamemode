<script setup>
import { fill } from "../shared/locale.js";
defineProps({ t: { type: Object, required: true }, view: { type: Object, required: true } });
const HANDS = ["Nothing", "Pair", "TwoPairs", "ThreeOfAKind", "SmallStraight", "BigStraight", "FullHouse", "FourOfAKind", "FiveOfAKind"];
</script>

<template>
  <article class="gambling__rules">
    <h2>{{ t.Poker }}</h2>
    <p>{{ fill(t['rules.Poker'], { rounds: view.roundsToWin }) }}</p>
    <h3>{{ t['rules.hands'] }}</h3>
    <ol class="gambling__ranks"><li v-for="hand in HANDS" :key="hand">{{ t[`hand.${hand}`] }}</li></ol>
    <p>{{ t['rules.tiebreak'] }}</p>
    <h2>{{ t.Favor }}</h2>
    <p>{{ t['rules.Favor'] }}</p>
    <p>{{ fill(t['rules.common'], { min: view.minStake, max: view.maxStake, cut: view.cutPercent, replays: view.maxReplays }) }}</p>
    <p>{{ fill(t['rules.time'], { invite: view.inviteSeconds, decision: view.decisionSeconds }) }}</p>
  </article>
</template>
