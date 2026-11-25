namespace CyrodiilRP.Server.Fishing;

// What a FishingLine came to in a call.
internal enum LineChange
{
    None,

    Cast,

    // A fish only tested the bait.
    Nibble,

    Bite,

    // A strike in time: the fish is on the hook.
    Hooked,

    // The landing went on: the bend or the fish's strength changed.
    Bend,

    Caught,

    // A strike before any bite.
    LostEarly,

    // A strike at a nibble, which spooked the fish.
    LostNibble,

    // No strike, or one too late.
    LostLate,

    // Bent too hard: the line broke.
    LostSnapped,

    // Held too slack for too long: the fish slipped off.
    LostSlack,

    // The rest is over: the angler may cast again.
    Ready,
}
