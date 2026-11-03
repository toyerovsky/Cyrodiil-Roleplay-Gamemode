import characters from "../../locales/en/characters.json";
import { fill } from "../shared/locale.js";

// Marius Varro is taken, any other name is accepted after a short wait. #waiting: hidden until a test shows it.
export function pretendGame(readym) {
  const waiting = location.hash === "#waiting";

  readym.onSend((name, args) => {
    if (name === "CreatorReady" && !waiting) readym.emit("CreatorShown", true);
    if (name !== "SubmitName") return;

    const [firstName, lastName] = args;
    const taken = `${firstName} ${lastName}`.toLowerCase() === "marius varro";
    setTimeout(() => {
      readym.emit("NameAnswer", taken ? fill(characters["characters.name_taken"], { name: "Marius Varro" }) : "");
      if (!taken) readym.emit("CreatorShown", false);
    }, 300);
  });
}
