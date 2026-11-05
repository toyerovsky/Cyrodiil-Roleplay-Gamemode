import audit from "../rp/art/icons/audit.webp";
import dice from "../rp/art/icons/dice.webp";
import items from "../rp/art/icons/items.webp";
import players from "../rp/art/icons/players.webp";
import sanctions from "../rp/art/icons/sanctions.webp";
import world from "../rp/art/icons/world.webp";

// The tabs in their order (AdminTabs.cs), each under a medallion of the game's own menus. The server names the ones the
// staff member's roles open.
export const TABS = [
  { id: "players", medallion: players },
  { id: "items", medallion: items },
  { id: "sanctions", medallion: sanctions },
  { id: "audit", medallion: audit },
  { id: "world", medallion: world },
  { id: "dice", medallion: dice },
];

// What the panel asks the server for apart from its state, named as AdminParts.cs names them (a test ties the two).
export const PARTS = { card: "card", sanctions: "sanctions", audit: "audit", items: "items", rolls: "rolls", lore: "lore" };
