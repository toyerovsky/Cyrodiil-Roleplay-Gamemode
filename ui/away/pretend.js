const ANSWER_MS = 50;

// "": 30 s before the mark, counting down. #away: marked, away for 4:51 and counting up. #long: away for over an hour.
const SCENARIOS = {
  "": ["Warning", 30],
  away: ["Marked", 291],
  long: ["Marked", 3725],
};

export function pretendGame(readym) {
  const [message, seconds] = SCENARIOS[location.hash.slice(1)] ?? SCENARIOS[""];
  readym.onSend((name) => {
    if (name === "AwayReady") setTimeout(() => readym.emit(message, seconds), ANSWER_MS);
  });
}
