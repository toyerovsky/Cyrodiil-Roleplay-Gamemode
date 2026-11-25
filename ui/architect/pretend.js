import building from "../../locales/en/building.json";

const ANSWER_MS = 50;
const STEPS = [1, 2, 4, 0];
// The game says a piece fits left of this much of the screen, and not right of it.
const FIT_EDGE = 0.8;
// A click left of this much of the screen picks the table.
const PICK_EDGE = 0.5;
const TILES_A_SCREEN = 40;

const FORM = {
  kind: "Residential",
  price: 5000,
  weeklyTax: 100,
  heightMetres: 15,
  mostTiles: 64,
  mostGold: 1000000,
  mostHeightMetres: 100,
  tileCentimetres: 182.857,
};

const CATEGORIES = ["Houses", "Floors", "Farm", "Fences", "Furniture", "Decoration"];

const PIECES = [
  { key: "house.farmhouse-1", name: "Farmhouse", category: "Houses", use: "Residential" },
  { key: "house.farmhouse-2", name: "Farmhouse, long", category: "Houses", use: "Residential" },
  { key: "house.farmhouse-3", name: "Farmhouse, two floors", category: "Houses", use: "Residential" },
  { key: "house.lower-1", name: "Town house", category: "Houses", use: "Residential" },
  { key: "house.lower-2", name: "Town house, narrow", category: "Houses", use: "Residential" },
  { key: "house.tent-1", name: "Tent", category: "Houses", use: "Any" },
  { key: "floor.brick-large", name: "Brick floor, large", category: "Floors", use: "Any" },
  { key: "floor.boardwalk", name: "Boardwalk", category: "Floors", use: "Any" },
  { key: "farm.stable", name: "Stable", category: "Farm", use: "Industrial" },
  { key: "farm.hay", name: "Hay bale", category: "Farm", use: "Any" },
  { key: "farm.well", name: "Well", category: "Farm", use: "Any" },
  { key: "fence.rail", name: "Rail fence", category: "Fences", use: "Any" },
  { key: "fence.rail-end", name: "Rail fence end post", category: "Fences", use: "Any" },
  { key: "furniture.bed", name: "Bed", category: "Furniture", use: "Any" },
  { key: "furniture.table", name: "Table", category: "Furniture", use: "Any" },
  { key: "furniture.chest", name: "Chest", category: "Furniture", use: "Any" },
  { key: "decoration.barrel", name: "Barrel", category: "Decoration", use: "Any" },
];

const PLOT = { id: 3, kind: "Residential", width: 12, depth: 8, heightMetres: 15, price: 5000, weeklyTax: 100, pieces: 4 };
const NEAR = [
  { id: 3, kind: "Residential", width: 12, depth: 8, pieces: 4, metres: 0 },
  { id: 7, kind: "Industrial", width: 20, depth: 16, pieces: 0, metres: 42 },
];

// A stand-in for the game's drawing of a piece: a shape in the piece's own colour.
const picture = (index) =>
  "data:image/svg+xml;base64," +
  btoa(
    `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64"><rect width="64" height="64" fill="#2f2416"/>` +
      `<path d="M10 50 L32 18 L54 50 Z" fill="hsl(${(index * 47) % 360} 35% 55%)"/></svg>`,
  );

const say = (key, args = {}) => building[key].replace(/\{(\w+)\}/g, (match, name) => (name in args ? String(args[name]) : match));

// #choose: /abuild off a plot, the plots near to choose from. #far: none near. #picked: the table on the plot is picked. #mark: the staff mark a new plot. #marked: one is marked, its form open.
// #look: B gave the game the view. #polish: the page in Polish. #blank: no picture drawn yet.
const SCENARIOS = {
  "": { tool: "Build" },
  choose: { tool: "Choose" },
  far: { tool: "Choose", far: true },
  blank: { tool: "Build", blank: true },
  picked: { tool: "Build", selected: "furniture.table" },
  mark: { tool: "Mark" },
  marked: { tool: "Mark", marked: [12, 8] },
  look: { tool: "Build", look: true },
  polish: { tool: "Build", locale: "pl" },
};

export function pretendGame(readym) {
  const scenario = SCENARIOS[location.hash.slice(1)] ?? SCENARIOS[""];
  const state = { tool: scenario.tool, plot: scenario.tool === "Build" ? { ...PLOT } : null };
  let step = 0;
  let carried = null;
  let fits = false;
  let selected = scenario.selected ?? "";
  let pending = null;
  let markFrom = null;

  const later = (act) => setTimeout(act, ANSWER_MS);
  const view = () => ({
    tool: state.tool,
    plot: state.plot,
    plots: state.tool === "Choose" && !scenario.far ? NEAR : [],
    pieces: PIECES,
    categories: CATEGORIES,
    form: FORM,
  });
  const send = () => later(() => readym.emit("State", JSON.stringify(view())));
  const answer = (text, done) => later(() => readym.emit("Answer", text, done));
  const fit = (x) => {
    fits = x < FIT_EDGE;
    readym.emit("Fit", fits ? "Fits" : "Outside");
  };
  const mark = (x, y) => {
    const width = Math.max(1, Math.round(Math.abs(x - markFrom[0]) * TILES_A_SCREEN));
    const depth = Math.max(1, Math.round(Math.abs(y - markFrom[1]) * TILES_A_SCREEN));
    readym.emit("Marked", width, depth);
  };
  const name = (key) => PIECES.find((piece) => piece.key === key)?.name ?? key;

  readym.onSend((message, args) => {
    const [x, y, piece] = args;
    switch (message) {
      case "ArchitectReady":
        if (scenario.locale) readym.emit("Locale", scenario.locale);
        readym.emit("Step", STEPS[step]);
        readym.emit("State", JSON.stringify(view()));
        if (selected) readym.emit("Selected", selected);
        if (scenario.marked) readym.emit("Marked", ...scenario.marked);
        if (scenario.look) readym.emit("Look", true);
        if (!scenario.blank) PIECES.forEach((each, index) => readym.emit("Picture", each.key, picture(index)));
        break;
      case "ArchitectGrab":
        if (piece) {
          carried = piece;
          fit(x);
        } else if (state.tool === "Mark") {
          markFrom = [x, y];
          readym.emit("Marked", 1, 1);
        } else {
          // A click picks, the piece is carried once the mouse moves
          selected = x < PICK_EDGE ? "furniture.table" : "";
          pending = selected || null;
          readym.emit("Selected", selected);
        }
        break;
      case "ArchitectPoint":
        if (pending) {
          carried = pending;
          pending = null;
        }
        if (carried) fit(x);
        else if (markFrom) mark(x, y);
        break;
      case "ArchitectDrop":
        if (carried && fits) {
          const moved = carried === selected;
          readym.emit("Fit", "None");
          if (!moved) state.plot.pieces += 1;
          answer(say(moved ? "building.moved" : "building.placed", { name: name(carried) }), true);
          if (!moved) send();
        } else if (carried) {
          readym.emit("Fit", "None");
        } else if (markFrom) {
          mark(x, y);
        }
        carried = null;
        pending = null;
        markFrom = null;
        break;
      case "ArchitectCancel":
        carried = null;
        readym.emit("Fit", "None");
        break;
      case "ArchitectTurn":
      case "ArchitectRaise":
        if (selected && !carried) answer(say("building.moved", { name: name(selected) }), true);
        break;
      case "ArchitectStep":
        step = (step + 1) % STEPS.length;
        readym.emit("Step", STEPS[step]);
        break;
      case "ArchitectSetStep":
        step = Math.max(0, STEPS.indexOf(args[0]));
        readym.emit("Step", STEPS[step]);
        break;
      case "ArchitectSetSettle":
      // The view moves only in the game
      case "ArchitectPan":
      case "ArchitectOrbit":
      case "ArchitectZoom":
        break;
      case "ArchitectNudge":
        answer(say("building.moved", { name: name(selected) }), true);
        break;
      case "ArchitectChoose": {
        const chosen = NEAR.find((plot) => plot.id === args[0]);
        state.tool = chosen ? "Build" : "Choose";
        state.plot = chosen ? { ...PLOT, id: chosen.id, kind: chosen.kind, width: chosen.width, depth: chosen.depth, pieces: chosen.pieces } : null;
        selected = "";
        send();
        break;
      }
      case "ArchitectNewPlot":
        state.tool = "Mark";
        state.plot = null;
        send();
        break;
      case "ArchitectCopy":
        break;
      case "ArchitectPaste":
      case "ArchitectDuplicate":
        state.plot.pieces += 1;
        answer(say("building.placed", { name: name(selected || "furniture.table") }), true);
        send();
        break;
      case "ArchitectSnapToFloor":
        answer(say("building.moved", { name: name(selected) }), true);
        break;
      case "ArchitectDelete":
        answer(say("building.removed", { name: name(selected) }), true);
        selected = "";
        readym.emit("Selected", "");
        state.plot.pieces -= 1;
        send();
        break;
      case "ArchitectDeselect":
        selected = "";
        readym.emit("Selected", "");
        break;
      case "ArchitectSavePlot": {
        const form = JSON.parse(args[0]);
        state.tool = "Build";
        state.plot = { ...PLOT, id: 4, kind: form.kind, price: form.price, weeklyTax: form.weeklyTax, heightMetres: form.heightMetres, pieces: 0 };
        answer(say("building.plot_made", { id: 4, width: 12, depth: 8 }), true);
        send();
        break;
      }
      case "ArchitectClearPlot":
        readym.emit("Marked", 0, 0);
        break;
      case "ArchitectLeave":
        setTimeout(() => readym.emit("Closed"), 0);
        break;
    }
  });
}
