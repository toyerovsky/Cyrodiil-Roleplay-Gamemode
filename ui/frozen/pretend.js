import sanctions from "../../locales/en/sanctions.json";
import { fill } from "../shared/locale.js";

const ANSWER_MS = 50;

// The staff froze the character as the page came up. #long has a reason that wraps.
export function pretendGame(readym) {
  const reason = location.hash.slice(1) === "long"
    ? "Running from the guards in the middle of a scene with six other players, after two warnings from the staff about it"
    : "Running from the guards mid scene";
  readym.onSend((name) => {
    if (name === "FrozenReady") setTimeout(() => readym.emit("Notice", fill(sanctions["sanctions.frozen_notice"], { reason })), ANSWER_MS);
  });
}
