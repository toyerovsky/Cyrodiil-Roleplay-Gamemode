import bank from "../../locales/en/bank.json";
import { fill } from "../shared/locale.js";

const ANSWER_MS = 50;

// A character with 1,250 gold at the bank and 340 carried. #empty: nothing at the bank yet and 85 carried. #rich: sums
// long enough to test the window's width.
const SCENARIOS = {
  "": { balance: 1250, gold: 340 },
  empty: { balance: 0, gold: 85 },
  rich: { balance: 98_765_432_100, gold: 2_147_483_000 },
};

export function pretendGame(readym) {
  const account = { ...(SCENARIOS[location.hash.slice(1)] ?? SCENARIOS[""]) };
  const send = () => setTimeout(() => readym.emit("State", JSON.stringify(account)), ANSWER_MS);
  const answer = (key, values, done) => setTimeout(() => readym.emit("Answer", fill(bank[`bank.${key}`], values), done), ANSWER_MS);

  readym.onSend((name, args) => {
    if (name === "BankReady") send();
    if (name === "BankClose") setTimeout(() => readym.emit("Closed"), 0);

    const [amount] = args;
    if (name === "BankDeposit") {
      if (amount > account.gold) {
        answer("too_poor", { amount, gold: account.gold }, false);
        return;
      }
      account.gold -= amount;
      account.balance += amount;
      answer("deposited", { amount }, true);
      send();
    }

    if (name === "BankWithdraw") {
      if (amount > account.balance) {
        answer("too_little", { amount, balance: account.balance }, false);
        return;
      }
      account.balance -= amount;
      account.gold += amount;
      answer("withdrawn", { amount }, true);
      send();
    }
  });
}
