using CyrodiilRP.Common.Building;

namespace CyrodiilRP.Server.Building;

// A player in architect mode. PlotId: the plot they build on, 0 while they choose one or mark a new one.
internal sealed record ArchitectSession(ArchitectTool Tool, long PlotId);
