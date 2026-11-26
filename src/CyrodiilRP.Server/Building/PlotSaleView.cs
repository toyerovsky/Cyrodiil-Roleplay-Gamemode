using CyrodiilRP.Data;

namespace CyrodiilRP.Server.Building;

// What a plot's sign shows: the plot (its size in tiles and metres, its height), what it costs, whose it is, and the
// reader's gold and plots. MostOwned: how many plots a character may own.
internal sealed record PlotSaleView(
    long Id,
    PlotKind Kind,
    int Width,
    int Depth,
    float WidthMetres,
    float DepthMetres,
    int HeightMetres,
    int Price,
    int WeeklyTax,
    PlotStanding Standing,
    int Gold,
    int Owned,
    int MostOwned);
