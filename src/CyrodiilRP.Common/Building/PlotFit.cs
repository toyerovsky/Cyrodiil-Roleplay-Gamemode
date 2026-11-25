namespace CyrodiilRP.Common.Building;

// Whether a piece's box stands within a plot.
public enum PlotFit
{
    Fits,

    // A corner of its box leaves the plot's tiles.
    Outside,

    // Its top reaches above the plot's height.
    TooHigh,
}
