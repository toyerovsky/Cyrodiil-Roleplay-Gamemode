namespace CyrodiilRP.Server.Building;

// What a new plot's marking came to.
internal enum MarkingVerdict
{
    Accepted,

    // Numbers missing or not numbers: a client's garbage, not a player's mistake.
    Malformed,

    // A side longer than the config allows.
    TooBig,

    // A kind outside the list, gold below 0 or past the most, a height outside its limits.
    FormInvalid,

    // The sign stands far from the plot's edge, or the ground far from the sign.
    SignFar,

    // It would cover another plot of the area.
    Overlaps,
}
