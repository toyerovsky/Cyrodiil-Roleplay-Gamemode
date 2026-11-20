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

const ANIMATION_MS = 3000;
const OPPONENT = "A wandering Breton from the mountains above Chorrol";
const event = (id, seat, round, kind, indices, values, set) => ({
  id, match: 81, seat, round, replay: 0, kind, indices, values, seed: 27181 + id * 997, set,
});

export function pretendGame(readym) {
  const scenario = location.hash.slice(1);
  const viewer = scenario === "seat1" ? 1 : 0;
  let state = structuredClone(INITIAL);
  let activeAt = 0;
  let nextId = 1;
  let visible = true;
  const other = 1 - viewer;
  const publish = () => {
    if (!visible) return;
    if (state.match?.activeThrow) state.match.throwElapsedMs = Date.now() - activeAt;
    readym.emit("State", JSON.stringify(state));
  };
  const send = () => setTimeout(publish, ANSWER_MS);
  const hands = () => {
    const match = state.match;
    const both = [[], []];
    for (const thrown of match.throws.filter((item) => item.round === match.round)) {
      if (thrown.kind === "Initial") both[thrown.seat] = [...thrown.values];
      thrown.indices.forEach((index, i) => { both[thrown.seat][index] = thrown.values[i]; });
    }
    match.yourHand = both[viewer];
    match.opponentHand = both[other];
  };
  function makeMatch(mode = "Poker", stake = 40) {
    return { ...structuredClone(MATCH), seat: viewer, turnSeat: 0, picks: [0, 0], throws: [], activeThrow: null,
      throwElapsedMs: 0, animationMs: ANIMATION_MS, roundWinner: -1, opponent: OPPONENT,
      you: viewer === 0 ? "Marcus Varro" : "Lucia Gallus", mode, stake, pot: stake * 2 };
  }
  function append(seat, kind, indices, values) {
    const match = state.match;
    match.throws.push(event(nextId++, seat, match.round, kind, indices, values, seat === 0 ? "imperial" : "daedric"));
    match.activeThrow = match.throws.at(-1).id;
    activeAt = Date.now();
    match.turnSeat = seat;
    hands();
    publish();
  }
  function sequence(kind, onDone) {
    const match = state.match;
    match.version++;
    match.phase = "Rolling";
    match.busy = true;
    match.leftMs = 0;
    const seats = [0, 1].filter((seat) => kind === "Initial" || match.picks[seat]);
    function next() {
      const seat = seats.shift();
      if (seat === undefined) {
        match.activeThrow = null;
        match.busy = false;
        match.leftMs = 45000;
        match.picks = [0, 0];
        match.version++;
        onDone();
        publish();
        return;
      }
      const count = match.mode === "Poker" ? 5 : 2;
      const indices = Array.from({ length: count }, (_, i) => i).filter((i) => kind === "Initial" || match.picks[seat] & (1 << i));
      const initialValues = match.mode === "Favor" ? (seat === viewer ? [6, 5] : [2, 3]) : (seat === viewer ? [4, 4, 1, 2, 6] : [5, 5, 2, 2, 6]);
      const values = kind === "Initial" ? initialValues.slice(0, count)
        : indices.map(() => seat === viewer ? 4 : 2);
      append(seat, kind, indices, values);
      setTimeout(next, ANIMATION_MS);
    }
    next();
  }
  function initial() {
    state.match.confirmed = false;
    sequence("Initial", () => {
      if (state.match.mode === "Favor") finish();
      else Object.assign(state.match, { phase: "Raise", yourTurn: true, turnSeat: viewer, yourRank: "Pair", opponentRank: "Two pairs" });
    });
  }
  function finish() {
    Object.assign(state.match, { phase: "Finished", winner: 0, roundWinner: viewer, wins: [2, 0],
      result: "You won the match. The pot is on its way.", yourRank: state.match.mode === "Poker" ? "Three of a kind" : "" });
  }
  function reroll() {
    const match = state.match;
    Object.assign(match, { phase: "Reroll", yourTurn: false, confirmed: false, picks: [0, 0] });
    for (const [delay, mask] of [[350, 2], [800, 10], [1200, 8]]) setTimeout(() => {
      if (match !== state.match || match.phase !== "Reroll") return;
      match.picks[other] = mask;
      publish();
    }, delay);
  }
  function score() {
    const match = state.match;
    if (match.round >= 2) { finish(); return; }
    Object.assign(match, { phase: "RoundResult", wins: [1, 0], roundWinner: viewer, yourRank: "Three of a kind", opponentRank: "Two pairs" });
    setTimeout(() => { match.round++; initial(); }, 4000);
  }
  if (scenario === "rules") state.rules = true;
  if (scenario === "empty") { state.table = null; state.opponents = []; }
  if (["raise", "call", "reroll", "waiting", "favor", "result", "seat1"].includes(scenario)) {
    state.match = makeMatch(scenario === "favor" ? "Favor" : "Poker");
    for (const seat of [0, 1]) {
      const values = (seat === viewer ? [4, 4, 1, 2, 6] : [5, 5, 2, 2, 6]).slice(0, state.match.mode === "Favor" ? 2 : 5);
      state.match.throws.push(event(nextId++, seat, 1, "Initial", values.map((_, i) => i), values, seat === 0 ? "imperial" : "daedric"));
    }
    hands();
    if (scenario === "call") state.match.phase = "Call";
    if (scenario === "reroll" || scenario === "waiting" || scenario === "seat1") state.match.phase = "Reroll";
    if (scenario === "waiting") state.match.confirmed = true;
    if (scenario === "favor") Object.assign(state.match, { phase: "RoundResult", yourRank: "", opponentRank: "", replay: 0 });
    if (scenario === "result") finish();
  }
  readym.onSend((name, args) => {
    if (name === "GamblingReady") { if (scenario === "seat1") reroll(); send(); }
    if (name === "GamblingClose") { visible = false; setTimeout(() => readym.emit("Closed"), 0); }
    if (name === "GamblingRefresh") { state = structuredClone(INITIAL); send(); }
    if (name === "GamblingInvite") {
      const [, stake, mode] = args;
      setTimeout(() => { state.match = makeMatch(mode, stake); initial(); }, 600);
    }
    if (name !== "GamblingAct") return;
    const [, , action, amount] = args;
    const match = state.match;
    if (action === "Select") { match.picks[viewer] = amount; send(); return; }
    match.version++;
    if (action === "Raise") {
      match.pot += amount;
      Object.assign(match, { phase: "Call", yourTurn: false, turnSeat: other, callAmount: amount });
      setTimeout(() => { match.pot += amount; reroll(); publish(); }, 700);
    }
    if (action === "Pass") Object.assign(match, { phase: "Call", yourTurn: true, turnSeat: viewer, callAmount: 10, pot: match.pot + 10 });
    if (action === "Call") { match.pot += match.callAmount; reroll(); }
    if (action === "Fold") Object.assign(match, { phase: "Finished", winner: 1, result: "You lost the match." });
    if (action === "Pick") {
      match.picks[viewer] = amount;
      match.confirmed = true;
      setTimeout(() => sequence("Reroll", score), 1400);
    }
    send();
  });
  window.addEventListener("keydown", (key) => {
    if (!visible && key.key?.toLowerCase() === "g") { visible = true; publish(); }
  });
}
