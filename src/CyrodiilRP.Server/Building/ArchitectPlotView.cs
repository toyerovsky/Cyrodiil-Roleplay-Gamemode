using CyrodiilRP.Data;

namespace CyrodiilRP.Server.Building;

// The plot architect mode builds on. Width and Depth in tiles, HeightMetres rounded.
internal sealed record ArchitectPlotView(long Id, PlotKind Kind, int Width, int Depth, int HeightMetres, int Price, int WeeklyTax, int Pieces);
