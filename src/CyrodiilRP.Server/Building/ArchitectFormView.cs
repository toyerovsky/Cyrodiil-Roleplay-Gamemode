using CyrodiilRP.Data;

namespace CyrodiilRP.Server.Building;

// A new plot's form: what it starts with, and the limits the page checks as the server does.
internal sealed record ArchitectFormView(
    PlotKind Kind,
    int Price,
    int WeeklyTax,
    int HeightMetres,
    int MostTiles,
    int MostGold,
    int MostHeightMetres,
    float TileCentimetres);
