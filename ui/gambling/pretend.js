const ANSWER_MS = 50;
const INITIAL = {
  table: 3, minStake: 1, maxStake: 500, cutPercent: 0, roundsToWin: 2, maxReplays: 3, inviteSeconds: 30, decisionSeconds: 45,
  rules: false, opponent: null, opponents: [
    { number: 3, name: "Lucia Gallus", refusal: null },
    { number: 7, name: "A wandering Breton from the mountains above Chorrol", refusal: null },
  ], match: null,
};
const MATCH = {
  id: 81, version: 1, mode: "Poker", phase: "Raise", busy: false, stake: 40, pot: 80, round: 1, replay: 0,
  wins: [0, 0], you: "Marcus Varro", opponent: "Lucia Gallus", yourHand: [4, 4, 1, 2, 6], opponentHand: [5, 5, 2, 2, 6],
  yourRank: "Pair", opponentRank: "Two pairs", yourTurn: true, confirmed: false, raiseLimit: 40, callAmount: 10, leftMs: 45000, winner: -1, result: null,
};
export const HISTORY = Array.from({ length: 10 }, (_, index) => ({
  id: 81 - index, at: Date.now() - index * 86400000, mode: index % 3 ? "Poker" : "Favor", first: "Marcus Varro", second: "Lucia Gallus",
  opponent: "Lucia Gallus", stake: 40, pot: 80 + index * 10, status: index === 0 ? "Open" : index % 4 === 0 ? "Refunded" : index % 5 === 0 ? "Cancelled" : "Settled",
  winner: index % 2 ? "Marcus Varro" : "Lucia Gallus", cut: 0,
}));

export function pretendGame(readym) {
  const scenario = location.hash.slice(1);
  let state = structuredClone(INITIAL);
  if (scenario === "rules") state.rules = true;
  if (scenario === "empty") { state.table = null; state.opponents = []; }
  if (["raise", "call", "reroll", "waiting", "favor", "result"].includes(scenario)) {
    state.match = structuredClone(MATCH);
    if (scenario === "call") state.match.phase = "Call";
    if (scenario === "reroll" || scenario === "waiting") state.match.phase = "Reroll";
    if (scenario === "waiting") state.match.confirmed = true;
    if (scenario === "favor") Object.assign(state.match, { mode: "Favor", phase: "RoundResult", yourHand: [6, 1], opponentHand: [3, 4], yourRank: "", opponentRank: "", replay: 1 });
    if (scenario === "result") Object.assign(state.match, { phase: "Finished", winner: 0, result: "You won the match. The pot is on its way." });
  }
  const send = () => setTimeout(() => readym.emit("State", JSON.stringify(state)), ANSWER_MS);
  readym.onSend((name, args) => {
    if (name === "GamblingReady") send();
    if (name === "GamblingClose") setTimeout(() => readym.emit("Closed"), 0);
    if (name === "GamblingRefresh") { state = structuredClone(INITIAL); send(); }
    if (name === "GamblingInvite") {
      const [opponent, stake, mode] = args;
      state.match = { ...structuredClone(MATCH), opponent: state.opponents.find((person) => person.number === opponent)?.name, mode, stake, pot: stake * 2 };
      if (mode === "Favor") Object.assign(state.match, { yourHand: [6, 5], opponentHand: [2, 3], yourRank: "", opponentRank: "", phase: "Finished", result: "You won the match." });
      send();
    }
    if (name === "GamblingAct") {
      const [, , action, amount] = args;
      const match = state.match;
      match.version++;
      if (action === "Raise") { match.pot += amount * 2; match.phase = "Reroll"; }
      if (action === "Pass" || action === "Call") match.phase = "Reroll";
      if (action === "Fold") Object.assign(match, { phase: "Finished", result: "You lost the match." });
      if (action === "Pick") {
        // The rerolled dice land on fours, so the hand shown matches its name: 4 4 1 2 6 becomes three fours.
        match.yourHand = match.yourHand.map((die, index) => amount & (1 << index) ? 4 : die);
        Object.assign(match, { phase: "Finished", round: 3, wins: [2, 1], yourRank: "Three of a kind", result: "You won the match. The pot is on its way." });
      }
      send();
    }
  });
}
