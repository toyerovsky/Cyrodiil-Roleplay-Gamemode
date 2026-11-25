namespace CyrodiilRP.Client.Building;

// What the mouse carries in architect mode.
internal enum ArchitectDrag
{
    None,

    // A piece out of the drawer.
    NewPiece,

    // A piece standing on the plot.
    MovePiece,

    // The corner of a new plot, from where the drag began.
    MarkPlot,
}
