// The pages, their preview scenarios (the hash) and the C# class each talks to.
export const PAGES = [
  { name: "chat", title: "Chat", scenarios: ["", "open", "long", "draft"], game: "Chat/ChatOverlay.cs" },
  { name: "creator", title: "New character", scenarios: ["", "waiting"], game: "Creator/NameFormOverlay.cs" },
  { name: "radial", title: "Wheel", scenarios: ["", "interaction", "staff"], game: "Interactions/RadialOverlay.cs" },
  { name: "exchange", title: "Trade", scenarios: ["", "offered", "bare"], game: "Exchange/ExchangeOverlay.cs" },
  { name: "party", title: "Party", scenarios: ["", "member"], game: "Party/PartyOverlay.cs" },
  { name: "asks", title: "Invitations", scenarios: ["", "trade", "outgoing", "introduce"], game: "Asks/AsksOverlay.cs" },
  { name: "trader", title: "Shop", scenarios: ["", "poor"], game: "Traders/TraderOverlay.cs" },
  { name: "menu", title: "Player menu", scenarios: ["", "help", "account", "dice", "lore"], game: "PlayerMenu/PlayerMenuOverlay.cs" },
  { name: "admin", title: "Staff panel", scenarios: ["", "card", "items", "offduty", "sanctions", "audit", "world", "nospawn", "dice"], game: "Admin/AdminOverlay.cs" },
  { name: "frozen", title: "Frozen", scenarios: ["", "long"], game: "Admin/FrozenOverlay.cs" },
  { name: "welcome", title: "Welcome", scenarios: ["", "first", "empty"], game: "Welcome/WelcomeOverlay.cs" },
  { name: "dice", title: "Dice board", scenarios: ["", "empty", "refuse", "crit", "present"], game: "Dice/DiceOverlay.cs" },
  { name: "lore", title: "Lore card", scenarios: ["", "empty", "view", "nocard", "readonly"], game: "Lore/LoreOverlay.cs" },
];
