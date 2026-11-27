using CyrodiilRP.Data;

namespace CyrodiilRP.Server.Building;

// A plot architect mode may open while choosing. Width and Depth in tiles, Metres from the architect to its edge.
internal sealed record ArchitectPlotChoice(long Id, PlotKind Kind, int Width, int Depth, int Pieces, int Metres);
