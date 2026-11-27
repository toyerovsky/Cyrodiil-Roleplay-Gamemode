const STATES = {
  "": { enabled: true, level: 1, xp: 250, next: 800, perkPoints: 0 },
  level: { enabled: true, level: 12, xp: 1200, next: 3100, perkPoints: 11 },
  max: { enabled: true, level: 40, xp: 0, next: 0, perkPoints: 39 },
  hidden: { enabled: false },
};
export function pretendGame(readym) {
  readym.onSend((name) => {
    if (name === "ProgressionReady") setTimeout(() => readym.emit("State", JSON.stringify(STATES[location.hash.slice(1)])), 0);
  });
}
