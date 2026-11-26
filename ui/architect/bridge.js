// Architect mode arrives as JSON: {tool (Choose, Mark or Build), plot: {id, kind, width, depth, heightMetres, price,
// weeklyTax, pieces} or null, plots: [{id, kind, width, depth, pieces, metres}] to choose from,
// kits: [{key, name}], categories, form: {kind, price, weeklyTax, heightMetres, mostTiles, mostGold,
// mostHeightMetres, tileCentimetres}}, again after each change the server took. The game says what the mouse carries
// fits (Fit), a turn it refused (Refused), the piece picked (Selected, its key or ""), the plot marked (Marked, width and
// depth in tiles, 0 for none), the grid (Step, steps a tile, 0 for none), when B lets the game have the view (Look) and
// each piece's picture as the game drew it (Picture, its key and a PNG data URL).
// Points are fractions of the screen, from its top left.
// The keys of the pieces shown go as one text, as ArchitectOverlay.KeySeparator splits it.
const KEY_SEPARATOR = ",";

export function createBridge(readym) {
  return {
    onLocale: (handler) => readym.on("Locale", handler),
    onState: (handler) => readym.on("State", (json) => handler(JSON.parse(json))),
    // The drawer's pieces in parts: the first starts the list again.
    onPieces: (handler) => readym.on("Pieces", (json, first) => handler(JSON.parse(json), first)),
    onAnswer: (handler) => readym.on("Answer", handler),
    onClosed: (handler) => readym.on("Closed", handler),
    onLook: (handler) => readym.on("Look", handler),
    onFit: (handler) => readym.on("Fit", handler),
    onRefused: (handler) => readym.on("Refused", handler),
    onSelected: (handler) => readym.on("Selected", handler),
    onMarked: (handler) => readym.on("Marked", handler),
    onStep: (handler) => readym.on("Step", handler),
    onPicture: (handler) => readym.on("Picture", handler),
    onBreakRefused: (handler) => readym.on("BreakRefused", handler),
    signalReady: () => readym.sendArchitectReady(),
    grab: (x, y, piece) => readym.sendArchitectGrab(x, y, piece),
    point: (x, y) => readym.sendArchitectPoint(x, y),
    drop: (x, y) => readym.sendArchitectDrop(x, y),
    cancel: () => readym.sendArchitectCancel(),
    turn: (degrees) => readym.sendArchitectTurn(degrees),
    raise: (centimetres) => readym.sendArchitectRaise(centimetres),
    step: () => readym.sendArchitectStep(),
    setStep: (steps) => readym.sendArchitectSetStep(steps),
    setSettle: (settle) => readym.sendArchitectSetSettle(settle),
    setHeight: (centimetres) => readym.sendArchitectSetHeight(centimetres),
    nudge: (forward, right) => readym.sendArchitectNudge(forward, right),
    remove: () => readym.sendArchitectDelete(),
    pan: (forward, right) => readym.sendArchitectPan(forward, right),
    orbit: (across, down) => readym.sendArchitectOrbit(across, down),
    zoom: (notches) => readym.sendArchitectZoom(notches),
    choose: (plot) => readym.sendArchitectChoose(plot),
    newPlot: () => readym.sendArchitectNewPlot(),
    removePlot: (plot) => readym.sendArchitectRemovePlot(plot),
    copy: () => readym.sendArchitectCopy(),
    paste: (x, y) => readym.sendArchitectPaste(x, y),
    duplicate: () => readym.sendArchitectDuplicate(),
    snapToFloor: () => readym.sendArchitectSnapToFloor(),
    turnFront: () => readym.sendArchitectTurnFront(),
    breakApart: () => readym.sendArchitectBreakApart(),
    showing: (keys) => readym.sendArchitectShowing(keys.join(KEY_SEPARATOR)),
    deselect: () => readym.sendArchitectDeselect(),
    savePlot: (form) => readym.sendArchitectSavePlot(form),
    clearPlot: () => readym.sendArchitectClearPlot(),
    leave: () => readym.sendArchitectLeave(),
  };
}
