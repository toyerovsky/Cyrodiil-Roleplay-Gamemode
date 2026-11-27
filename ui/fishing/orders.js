const PULLS = new Set(["Rest", "Left", "Right", "Away"]);

// What the fish's pull asks of the angler now and how they answer it. state: "do" what is still to be done, "done"
// what they are doing, "wrong" what loses the fish. key: the key the order is about, null when it asks for none.
// letGo: the key is to be let go, not pressed. text: the page's text that says it.
export function findOrder(line) {
  if (!PULLS.has(line.pull)) throw new Error(`The fish pulls "${line.pull}", which the page does not know.`);

  if (line.reeling && line.pull !== "Rest") return { state: "wrong", key: "space", letGo: true, text: "order.force" };

  switch (line.pull) {
    case "Rest":
      return { state: line.reeling ? "done" : "do", key: "space", letGo: false, text: line.reeling ? "order.Rest.done" : "order.Rest" };
    case "Left":
      return leanAgainst(line.lean, 1, "d", "order.Left");
    case "Right":
      return leanAgainst(line.lean, -1, "a", "order.Right");
    default:
      return { state: "done", key: null, letGo: false, text: "order.Away" };
  }
}

// against: the lean that answers the run. The other one gives the fish its strength back.
function leanAgainst(lean, against, key, text) {
  if (lean === against) return { state: "done", key, letGo: false, text: "order.held" };
  if (lean === -against) return { state: "wrong", key, letGo: false, text: `${text}.wrong` };
  return { state: "do", key, letGo: false, text };
}
