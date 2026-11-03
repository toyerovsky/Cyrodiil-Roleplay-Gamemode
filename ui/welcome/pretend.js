const ANSWER_MS = 50;
const DAY_MS = 24 * 60 * 60 * 1000;
const PLAYED_SECONDS = 26_712;
const NICKNAME = "toyer";

// Marcus Varro's player back after two days. #first: a first visit, no character yet. #empty: back, but the character
// was never finished. Play and New character hand over to the game, which closes the page.
export function pretendGame(readym) {
  const scenario = location.hash.slice(1);
  const characters = scenario === "" ? [{ firstName: "Marcus", lastName: "Varro", lastPlayedAt: Date.now() - 2 * DAY_MS, playedSeconds: PLAYED_SECONDS }] : [];
  const view = { nickname: NICKNAME, firstVisit: scenario === "first", characters, emptySlots: characters.length ? 0 : 1, lockedSlots: 1 };

  readym.onSend((name) => {
    if (name === "WelcomeReady") setTimeout(() => readym.emit("Welcome", JSON.stringify(view)), ANSWER_MS);
    if (name === "WelcomePlay" || name === "WelcomeNew") setTimeout(() => readym.emit("Closed"), ANSWER_MS);
  });
}
