import chat from "../../locales/en/chat.json";
import staff from "../../locales/en/staff.json";
import { fill } from "../shared/locale.js";

const NAME = "Marius Varro";
const LONG_HISTORY = 40;

// K opens the chat. #open: chat open at start. #long: more history than the log holds. #draft: open with a command the
// wheel started.
export function pretendGame(readym) {
  const scenario = location.hash.slice(1);
  let open = false;
  const line = (kind, text) => ({ kind, text, at: Date.now() });
  const say = (kind, key, args) => line(kind, fill(chat[key], args));

  const setOpen = (value) => {
    open = value;
    readym.emit("Open", value);
  };

  const history = () => {
    const lines = [
      line("System", "Welcome back, Marius Varro."),
      say("Say", "chat.say", { name: "Lucia Gallus", text: "Fresh bread, two septims a loaf!" }),
      say("Action", "chat.me", { name: "Ra'jirr", action: "leans on the market stall and counts his coins." }),
      say("Ambient", "chat.do", { description: "The smell of baking drifts from the Merchants Inn.", name: NAME }),
      say("LocalOoc", "chat.local_ooc", { number: 3, name: "toyer", text: "brb, coffee" }),
      say("Shout", "chat.shout", { name: "Guard Captain", text: "Stop right there, criminal scum!" }),
      line("Staff", fill(staff["staff.chat"], { name: "toyer", text: "Market event at dusk." })),
    ];
    if (scenario !== "long") return lines;
    const earlier = Array.from({ length: LONG_HISTORY }, (_, i) => say("Say", "chat.say", { name: "Town crier", text: `Hear ye, number ${i + 1}!` }));
    return [...earlier, ...lines];
  };

  readym.onSend((name, args) => {
    if (name === "RequestHistory") {
      readym.emit("History", JSON.stringify(history()));
      if (scenario === "open" || scenario === "long" || scenario === "draft") setOpen(true);
      if (scenario === "draft") readym.emit("Draft", "/pm 3 ");
    } else if (name === "Submit") {
      const [text] = args;
      const answer = text.startsWith("/")
        ? line("Rejection", fill(chat["chat.empty"], { example: chat["chat.example.me"] }))
        : say("Say", "chat.say", { name: NAME, text });
      readym.emit("Line", JSON.stringify([answer]));
    } else if (name === "Close" || name === "Blurred") {
      setOpen(false);
    }
  });

  document.addEventListener("keydown", (event) => {
    if (open || event.key.toLowerCase() !== "k") return;
    event.preventDefault();
    setOpen(true);
  });
}
