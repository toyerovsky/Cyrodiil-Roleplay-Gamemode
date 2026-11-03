import exchange from "../../locales/en/exchange.json";
import { fill } from "../shared/locale.js";

const CARRIED = 120;
const CONFIRM_DELAY_MS = 3000;
const ANSWER_MS = 50;
const LUCIA_CONFIRMS_MS = 600;
const DONE_SHOWS_MS = 2000;
const LUCIA_OFFERS = 40;

// The trade with Lucia Gallus, the server and Lucia both played here. She confirms once the player has. #offered: she
// has already put 40 gold in and confirmed. T opens the window again after it closed.
export function pretendGame(readym) {
  const scenario = location.hash.slice(1);
  let deal = null;

  const schedule = (ms, run) => setTimeout(run, ms);
  const view = () => ({
    version: deal.version,
    other: "Lucia Gallus",
    carried: CARRIED,
    max: CARRIED,
    confirmInMs: Math.max(0, deal.changedAt + CONFIRM_DELAY_MS - Date.now()),
    mine: { gold: deal.mine, confirmed: deal.mineConfirmed },
    theirs: { gold: deal.theirs, confirmed: deal.theirsConfirmed },
  });
  const send = () => schedule(ANSWER_MS, () => readym.emit("State", JSON.stringify(view())));
  const markChanged = () => {
    deal.version++;
    deal.changedAt = Date.now();
    deal.mineConfirmed = false;
    deal.theirsConfirmed = false;
  };

  const open = () => {
    const offered = scenario === "offered";
    deal = { version: 1, changedAt: Date.now(), mine: 0, theirs: offered ? LUCIA_OFFERS : 0, mineConfirmed: false, theirsConfirmed: offered };
    send();
  };

  const finish = () => {
    const received = deal.theirs - deal.mine;
    const text = received > 0
      ? fill(exchange["exchange.received"].other, { count: received })
      : received < 0 ? fill(exchange["exchange.gave"].other, { count: -received }) : exchange["exchange.even"];
    deal = null;
    readym.emit("Done", text);
    schedule(DONE_SHOWS_MS, () => readym.emit("Closed"));
  };

  readym.onSend((name, args) => {
    if (name === "ExchangeReady") open();
    if (!deal) return;

    if (name === "ExchangeOffer") {
      const [gold] = args;
      if (gold < 0 || gold > CARRIED) {
        readym.emit("Refused", fill(exchange["exchange.amount"], { max: CARRIED }));
        send();
        return;
      }
      if (gold === deal.mine) return;
      deal.mine = gold;
      markChanged();
      send();
    } else if (name === "ExchangeConfirm") {
      const [version] = args;
      if (version !== deal.version) {
        readym.emit("Refused", exchange["exchange.changed"]);
        send();
        return;
      }
      deal.mineConfirmed = true;
      if (deal.theirsConfirmed) {
        finish();
        return;
      }
      send();
      schedule(LUCIA_CONFIRMS_MS, () => {
        if (deal?.mineConfirmed) finish();
      });
    } else if (name === "ExchangeCancel") {
      deal = null;
      readym.emit("Closed");
    }
  });

  document.addEventListener("keydown", (event) => {
    if (deal || event.key.toLowerCase() !== "t") return;
    event.preventDefault();
    open();
  });
}
