const PULLS = new Set(["Rest", "Left", "Right", "Away"]);
// The lean that answers a run to a side (the other one gives the fish its strength back), its key, and the side of the
// float the key is drawn on: the one the rod goes to, across the float from the run's arrow.
const ANSWERS = Object.freeze({
  Left: { lean: 1, key: "d", side: "right", text: "order.Left" },
  Right: { lean: -1, key: "a", side: "left", text: "order.Right" },
});

// What the fish's pull asks of the angler now and how they answer it, for the plate. state: "do" what is still to be
// done, "done" what they are doing, "wrong" what loses the fish. key: the key the order is about. letGo: the key is to
// be let go or kept so, not pressed. text: the page's text that says it.
export function findOrder(line) {
  check(line);
  if (isForced(line)) return { state: "wrong", key: "space", letGo: true, text: "order.force" };
  if (line.pull === "Rest") return { state: line.reeling ? "done" : "do", key: "space", letGo: false, text: line.reeling ? "order.Rest.done" : "order.Rest" };

  const answer = ANSWERS[line.pull];
  if (!answer) return { state: "done", key: "space", letGo: true, text: "order.Away" };

  const state = findLeanState(line.lean, answer.lean);
  const text = { do: answer.text, done: "order.held", wrong: `${answer.text}.wrong` }[state];
  return { state, key: answer.key, letGo: false, text };
}

// The same at the float, where the eyes are. hold: the key to hold with the side of the float it is drawn on ("left",
// "right", "under") and its state as an order's, null when the pull asks for no key. letGo: Space is held into a run or
// a dive and must be let go, whatever else is held. free: a dive with nothing held, which is all it asks.
export function findCue(line) {
  check(line);
  if (line.pull === "Rest") return { hold: { key: "space", side: "under", state: line.reeling ? "done" : "do" }, letGo: false, free: false };

  const letGo = isForced(line);
  const answer = ANSWERS[line.pull];
  if (!answer) return { hold: null, letGo, free: !letGo };

  return { hold: { key: answer.key, side: answer.side, state: findLeanState(line.lean, answer.lean) }, letGo, free: false };
}

function check(line) {
  if (!PULLS.has(line.pull)) throw new Error(`The fish pulls "${line.pull}", which the page does not know.`);
}

// Reeling into a run or a dive snaps the line.
function isForced(line) {
  return line.reeling && line.pull !== "Rest";
}

function findLeanState(lean, against) {
  if (lean === against) return "done";
  return lean === -against ? "wrong" : "do";
}
