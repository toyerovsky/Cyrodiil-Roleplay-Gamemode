const ANSWER_MS = 50;
const STREAK_SECONDS = 287;

// Inside the Sanctuary and protected. #aggressor: inside after an attack, the streak running.
const STATES = {
  "": { inside: true, aggressor: false, secondsLeft: 0 },
  aggressor: { inside: true, aggressor: true, secondsLeft: STREAK_SECONDS },
};

export function pretendGame(readym) {
  const state = STATES[location.hash.slice(1)] ?? STATES[""];
  readym.onSend((name) => {
    if (name === "SanctuaryReady") setTimeout(() => readym.emit("State", JSON.stringify(state)), ANSWER_MS);
  });
}
