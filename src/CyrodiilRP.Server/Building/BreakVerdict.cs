namespace CyrodiilRP.Server.Building;

// What the parts of a piece taken apart came to.
internal enum BreakVerdict
{
    Accepted,

    // Numbers missing or not numbers: a client's garbage, not a player's mistake.
    Malformed,

    // More parts than ArchitectRules.MostParts.
    TooMany,

    // A part no mesh piece of the catalogue stands as.
    UnknownPart,
}
