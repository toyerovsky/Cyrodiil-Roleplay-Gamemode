using CyrodiilRP.Data;

namespace CyrodiilRP.Server.Building;

// A new plot's form as the page sent it, not checked yet.
internal sealed record PlotForm(PlotKind Kind, int Price, int WeeklyTax, int HeightMetres);
