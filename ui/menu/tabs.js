import account from "../rp/art/icons/account.webp";
import character from "../rp/art/icons/character.webp";
import help from "../rp/art/icons/help.webp";
import keys from "../rp/art/icons/keys.webp";

// The tabs in their order, each under a medallion of the game's own menus. The server opens the menu on the first three
// (MenuTabs.cs).
export const TABS = [
  { id: "character", medallion: character },
  { id: "account", medallion: account },
  { id: "help", medallion: help },
  { id: "keys", medallion: keys },
];

// The keys the mode binds in the game (the client's Key.X), and Esc, which every window of it closes on.
export const KEYS = [
  { id: "self_wheel", key: "G" },
  { id: "interaction_wheel", key: "H" },
  { id: "accept", key: "Y" },
  { id: "decline", key: "N" },
  { id: "chat", key: "K" },
  { id: "menu", key: "F10" },
  { id: "close", key: "Esc" },
];
