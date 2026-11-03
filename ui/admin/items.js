// The record types of the game's items (items.json), in the order the items tab offers them. A test ties them to the
// catalogue the server ships.
export const ITEM_TYPES = ["WEAP", "ARMO", "CLOT", "ALCH", "INGR", "BOOK", "SGST", "SLGM", "AMMO", "APPA", "MISC", "KEYM", "LIGH"];

// Counts one click picks.
export const QUICK_COUNTS = [1, 5, 10, 25, 100];

// The items given last, kept while the game runs, for a second give with one click.
export const RECENT_KEPT = 8;

// A search starts at this many letters, fewer would list the whole game. A type alone lists that type.
export const SEARCH_FROM = 2;

// The panel waits for the typing to stop before it asks the server.
export const SEARCH_AFTER_MS = 250;

// An item the catalogue does not know shows as its form id in hex, which /aitem does not take.
export const isUnknownItem = (entry) => entry.id === entry.name && /^[0-9A-F]{8}$/.test(entry.id);
