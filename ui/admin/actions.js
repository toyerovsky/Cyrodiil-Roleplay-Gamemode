import { fill } from "../shared/locale.js";

// The panel's buttons: the staff command each runs (the server runs only those of AdminActions.cs, tied by a test), the
// line it sends, and what the staff member fills in first. The rest of the line comes from where the button stands.
export const ACTIONS = {
  duty: { command: "duty", line: "/duty" },
  goto: { command: "agoto", line: "/agoto {number}" },
  bring: { command: "abring", line: "/abring {number}" },
  back: { command: "aback", line: "/aback {number}" },
  heal: { command: "aheal", line: "/aheal {number}" },
  feed: { command: "afeed", line: "/afeed {number}" },
  health: { command: "ahp", line: "/ahp {number} {health}", fields: ["health"] },
  message: { command: "amsg", line: "/amsg {number} {text}", fields: ["text"] },
  revive: { command: "arevive", line: "/arevive {number}" },
  freeze: { command: "afreeze", line: "/afreeze {number} {reason}", fields: ["reason"] },
  unfreeze: { command: "aunfreeze", line: "/aunfreeze {number}" },
  mute: { command: "amute", line: "/amute {number} {minutes} {reason}", fields: ["minutes", "reason"] },
  unmute: { command: "aunmute", line: "/aunmute {number}" },
  warn: { command: "awarn", line: "/awarn {number} {reason}", fields: ["reason"] },
  kick: { command: "akick", line: "/akick {number} {reason}", fields: ["reason"] },
  ban: { command: "aban", line: "/aban {number} {minutes} {reason}", fields: ["minutes", "reason"] },
  gold: { command: "agold", line: "/agold {number} {amount}", fields: ["amount"] },
  roles: { command: "aroles", line: "/aroles {number} {roles}", fields: ["roles"] },
  give: { command: "aitem", line: "/aitem give {number} {item} {count}", fields: ["count"] },
  take: { command: "aitem", line: "/aitem take {number} {item} {count}", fields: ["count"] },
  giveNow: { command: "aitem", line: "/aitem give {number} {item} {count}" },
  liftBan: { command: "aunban", line: "/aunban {account}" },
  liftMute: { command: "aunmute", line: "/aunmute {who}" },
  liftFreeze: { command: "aunfreeze", line: "/aunfreeze {who}" },
  place: { command: "atp", line: "/atp {place}" },
  spot: { command: "atp", line: "/atp {x} {y} {z}" },
  time: { command: "atime", line: "/atime {hour}" },
  weather: { command: "aweather", line: "/aweather {weather}" },
  spawnHere: { command: "aspawn", line: "/aspawn here" },
  spawnGo: { command: "aspawn", line: "/aspawn go" },
  loreClear: { command: "alore", line: "/alore {number} clear {field}" },
  factionDissolve: { command: "afaction", line: "/afaction dissolve #{id} {reason}", fields: ["reason"] },
  factionRename: { command: "afaction", line: "/afaction rename #{id} {factionName}", fields: ["factionName"] },
  factionLead: { command: "afaction", line: "/afaction lead #{id} #{member}" },
  factionRemove: { command: "afaction", line: "/afaction remove #{id} #{member}" },
  noticeRemove: { command: "anotice", line: "/anotice remove {id}" },
};

// The card's buttons in their order. A pair shows the one that fits: a frozen player gets Unfreeze, not Freeze.
export const PLAYER_ACTIONS = [
  "goto", "bring", "back", "heal", "feed", "health", "revive", "message", "freeze", "unfreeze", "warn", "mute", "unmute", "gold", "roles", "kick", "ban",
];

// What a button does to a character in the world: a player away gets none of them.
export const WORLD_ACTIONS = ["goto", "bring", "back", "heal", "feed", "health", "revive"];

// The lift of each kind of sanction the sanctions tab lists.
export const LIFTS = { ban: "liftBan", "admin.mute": "liftMute", "admin.freeze": "liftFreeze" };

export const buildLine = (action, values) => fill(ACTIONS[action].line, values);
