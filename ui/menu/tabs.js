import account from "../rp/art/icons/account.webp";
import character from "../rp/art/icons/character.webp";
import dice from "../rp/art/icons/dice.webp";
import help from "../rp/art/icons/help.webp";
import keys from "../rp/art/icons/keys.webp";
import lore from "../rp/art/icons/lore.webp";
import faction from "../rp/art/icons/players.webp";

// The tabs in their order, each under a medallion of the game's own menus. The server opens the menu on all but the keys
// (MenuTabs.cs).
export const TABS = [
  { id: "character", medallion: character },
  { id: "account", medallion: account },
  { id: "help", medallion: help },
  { id: "dice", medallion: dice },
  { id: "lore", medallion: lore },
  { id: "faction", medallion: faction },
  { id: "keys", medallion: keys },
];

// The keys the mode binds in the game (the client's Key.X), and Esc, which every window of it closes on. Label: how the key
// reads on the keyboard, where its name in the game's list does not. One bound key may do two things, held with Shift.
export const KEYS = [
  { id: "self_wheel", key: "G" },
  { id: "interaction_wheel", key: "H" },
  { id: "accept", key: "Y" },
  { id: "decline", key: "N" },
  { id: "chat", key: "K" },
  { id: "menu", key: "F10" },
  { id: "dice", key: "HOME", label: "Home" },
  { id: "lore", key: "END", label: "End" },
  { id: "lore_look", key: "END", label: "Shift+End" },
  { id: "close", key: "Esc" },
];
