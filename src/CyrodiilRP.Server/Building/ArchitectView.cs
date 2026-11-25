using CyrodiilRP.Common.Building;

namespace CyrodiilRP.Server.Building;

// What the architect page shows. Plot: null while choosing or marking. Plots: the ones within reach while choosing, the
// nearest first. Form: what a new plot starts with, and its limits.
internal sealed record ArchitectView(
    ArchitectTool Tool,
    ArchitectPlotView? Plot,
    IReadOnlyList<ArchitectPlotChoice> Plots,
    IReadOnlyList<ArchitectPieceView> Pieces,
    IReadOnlyList<PieceCategory> Categories,
    ArchitectFormView Form);
