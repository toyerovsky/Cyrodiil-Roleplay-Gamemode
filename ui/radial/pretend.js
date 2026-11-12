import admin from "../../locales/en/admin.json";
import chat from "../../locales/en/chat.json";
import dice from "../../locales/en/dice.json";
import emotes from "../../locales/en/emotes.json";
import exchange from "../../locales/en/exchange.json";
import feeding from "../../locales/en/feeding.json";
import lore from "../../locales/en/lore.json";
import party from "../../locales/en/party.json";
import sanctions from "../../locales/en/sanctions.json";
import stable from "../../locales/en/stable.json";
import { fill } from "../shared/locale.js";

const SELF_KEY = "g";
const EMOTES = ["Beckon", "Halt", "Salute", "Cheer", "Point", "Point up", "Offer", "Proclaim", "Exasperated", "Exhausted",
  "Sulk", "Look around", "Ponder", "Genuflect", "Yield", "Mages Guild salute", "Dance", "Juggle", "Take notes", "Pray"];
const INTERACTION_KEY = "h";
const ANSWER_MS = 50;

const option = (id, label, icon, reason = "") => ({ id, label, icon, disabled: reason !== "", reason });

const selfWheel = (withStaff) => ({
  title: "Marius Varro",
  items: [
    option("self.emotes", emotes["emotes.option"], "emote"),
    option("self.roll", dice["dice.option.board"], "dice"),
    option("self.lore", lore["lore.option.card"], "book"),
    option("self.horse", stable["stable.option.call"], "horse"),
    ...(withStaff ? [option("self.admin", admin["admin.option.panel"], "card")] : []),
  ],
});

// Stop and twenty emotes: three pages, the last of five.
const emoteWheel = () => ({
  title: emotes["emotes.title"],
  items: [
    option("stop", emotes["emotes.stop"], "stop"),
    ...EMOTES.map((name) => option(`emote:${name.toLowerCase().replaceAll(" ", "_")}`, name, "emote")),
  ],
});

const interactionWheel = (withStaff) => ({
  title: "Lucia Gallus",
  items: [
    option("player.greet", chat["chat.option.greet"], "hand"),
    option("player.lore", lore["lore.option.read"], "book"),
    option("player.pm", chat["chat.option.pm"], "letter"),
    option("player.trade", exchange["exchange.option"], "scales", fill(exchange["exchange.target_busy"], { name: "Lucia Gallus" })),
    option("player.feed", feeding["feeding.option"], "bread"),
    option("player.party_invite", party["party.option.invite"], "banner"),
    ...(withStaff
      ? [
          option("player.heal", admin["admin.option.heal"], "heart"),
          option("player.admin", admin["admin.option.card"], "card"),
          option("player.mute", sanctions["sanctions.option.mute"], "mute"),
          option("player.kick", sanctions["sanctions.option.kick"], "door"),
        ]
      : []),
  ],
});

// G opens the own wheel and H the one over Lucia Gallus, who is busy with another trade. #interaction starts on that one,
// #staff on it too with a staff member's options, two pages of them, #emotes on the emotes, which G opens there.
export function pretendGame(readym) {
  const scenario = location.hash.slice(1);
  const withStaff = scenario === "staff";
  const ownWheel = () => (scenario === "emotes" ? emoteWheel() : selfWheel(withStaff));
  let open = false;

  const show = (view, key) => {
    open = true;
    setTimeout(() => readym.emit("Wheel", JSON.stringify(view), key), ANSWER_MS);
  };

  readym.onSend((name) => {
    if (name === "WheelReady") {
      if (scenario === "interaction" || withStaff) show(interactionWheel(withStaff), INTERACTION_KEY);
      else show(ownWheel(), SELF_KEY);
    } else if (name === "WheelPick" || name === "WheelClose") {
      open = false;
    }
  });

  document.addEventListener("keydown", (event) => {
    if (open) return;
    if (event.key.toLowerCase() === SELF_KEY) show(ownWheel(), SELF_KEY);
    else if (event.key.toLowerCase() === INTERACTION_KEY) show(interactionWheel(withStaff), INTERACTION_KEY);
    else return;
    event.preventDefault();
  });
}
