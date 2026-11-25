namespace CyrodiilRP.Server.Fishing;

// How a bait is spent.
internal enum BaitUse
{
    // One piece goes with every cast.
    PerCast,

    // It stays on the line, and goes only when the line snaps or runs out.
    LostWithLine,
}
