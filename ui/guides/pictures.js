import bank from "../rp/art/guides/bank.webp";
import dice from "../rp/art/guides/dice.webp";
import exchange from "../rp/art/guides/exchange.webp";
import foreman from "../rp/art/guides/foreman.webp";
import gambling from "../rp/art/guides/gambling.webp";
import lore from "../rp/art/guides/lore.webp";
import mentor from "../rp/art/guides/mentor.webp";
import noticeBoard from "../rp/art/guides/notice-board.webp";
import stable from "../rp/art/guides/stable.webp";
import trader from "../rp/art/guides/trader.webp";
import welcome from "../rp/art/guides/welcome.webp";

// A picture for every guide, by its topic as the server names it (GuideTopic.cs): sepia ink sketches made for the mode
// (design/README.md).
export const PICTURES = {
  Welcome: welcome,
  Trader: trader,
  Bank: bank,
  Stable: stable,
  Mentor: mentor,
  Foreman: foreman,
  NoticeBoard: noticeBoard,
  Gambling: gambling,
  Dice: dice,
  Lore: lore,
  Exchange: exchange,
};

const NUMERALS = ["I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI", "XII"];

export const numeral = (index) => NUMERALS[index] ?? String(index + 1);
