namespace CyrodiilRP.Server.Building;

// What an architect did to a plot's pieces, as Ctrl+Z takes it back.
internal enum ArchitectChangeKind
{
    // Undone by taking the piece away.
    Placed,

    // Undone by moving it back.
    Moved,

    // Undone by placing it again where it stood.
    Removed,

    // Undone by taking its parts away and placing the whole again.
    BrokeApart,
}
