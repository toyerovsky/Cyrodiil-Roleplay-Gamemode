import notices from "../../locales/en/notices.json";

const ANSWER_MS = 50;
const COOLDOWN_SECONDS = 300;
const RULES = { shown: 10, minLength: 1, maxLength: 300, perCharacter: 3, expiryDays: 7 };

const LONG =
  "Wanted: a steady hand with a bow for the caravan to Bravil, leaving the Imperial City at dawn three days from now. " +
  "Fair pay in gold and a share of what we sell on the way. Ask at the Merchants Inn for Jeanne, and bring your own arrows, " +
  "we are not a charity. Half the pay before, half in Bravil.";

const SOME = [
  { id: 14, text: "Lost: a silver ring with a blue stone, somewhere between the Market District and the Arena.\nA reward for its return.", ageMinutes: 25, mine: false },
  { id: 12, text: "The Gilded Carafe buys wine by the barrel. Ask for Claudette.", ageMinutes: 190, mine: true },
  { id: 9, text: "Who painted the statue of Talos green? The Watch would like a word.", ageMinutes: 2900, mine: false },
];

const FULL = Array.from({ length: RULES.shown }, (_, index) => ({
  id: 40 - index,
  text: index % 3 === 0 ? LONG : `Notice number ${10 - index}: a room to let above the Tiber Septim Hotel, quiet and dry.`,
  ageMinutes: index * 300,
  mine: index === 2,
}));

// #empty: nothing pinned yet. #full: a board of ten, three of them as long as a notice may be. #limit: the reader has as
// many up as one may. #wait: the reader pinned one a minute ago.
const SCENARIOS = {
  "": { notices: SOME, mine: 1, waitSeconds: 0 },
  empty: { notices: [], mine: 0, waitSeconds: 0 },
  full: { notices: FULL, mine: 1, waitSeconds: 0 },
  limit: { notices: SOME.map((notice) => ({ ...notice, mine: true })), mine: 3, waitSeconds: 0 },
  wait: { notices: SOME, mine: 1, waitSeconds: COOLDOWN_SECONDS - 60 },
};

export function pretendGame(readym) {
  const scenario = SCENARIOS[location.hash.slice(1)] ?? SCENARIOS[""];
  const board = { board: 3, ...RULES, ...scenario, notices: [...scenario.notices] };
  let nextId = 100;

  const send = () => setTimeout(() => readym.emit("State", JSON.stringify(board)), ANSWER_MS);
  const answer = (text, done) => setTimeout(() => readym.emit("Answer", text, done), ANSWER_MS);

  readym.onSend((name, args) => {
    if (name === "BoardReady") send();
    if (name === "BoardClose") setTimeout(() => readym.emit("Closed"), 0);

    if (name === "BoardPost") {
      const text = args[0].trim();
      if (board.mine >= board.perCharacter) {
        answer(notices["notices.too_many"].other.replace("{count}", board.perCharacter), false);
        return;
      }
      board.notices = [{ id: nextId++, text, ageMinutes: 0, mine: true }, ...board.notices].slice(0, board.shown);
      board.mine += 1;
      board.waitSeconds = COOLDOWN_SECONDS;
      answer(notices["notices.posted"], true);
      send();
    }

    if (name === "BoardTakeDown") {
      board.notices = board.notices.filter((notice) => notice.id !== args[0]);
      board.mine -= 1;
      answer(notices["notices.taken_down"], true);
      send();
    }
  });
}
