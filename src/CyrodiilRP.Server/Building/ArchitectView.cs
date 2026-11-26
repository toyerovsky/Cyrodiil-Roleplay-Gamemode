using CyrodiilRP.Common.Building;

namespace CyrodiilRP.Server.Building;

// What the architect page shows, the drawer's pieces apart (ArchitectPieceParts). Plot: null while choosing or marking.
// Plots: the ones within reach while choosing, the nearest first. Form: what a new plot starts with, and its limits.
internal sealed record ArchitectView(
    ArchitectTool Tool,
    ArchitectPlotView? Plot,
    IReadOnlyList<ArchitectPlotChoice> Plots,
    IReadOnlyList<ArchitectKitView> Kits,
    IReadOnlyList<PieceCategory> Categories,
    ArchitectFormView Form);
