namespace CyrodiilRP.Server.Fishing;

// What a FishingLine came to in a call.
internal enum LineChange
{
    None,

    Cast,

    // A fish comes to look at the bait, a while before the first nibble or the bite.
    Approach,

    // A fish only tested the bait.
    Nibble,

    Bite,

    // A strike in time: the fish is on the hook.
    Hooked,

    // The fight went on: the tension, the fish's stamina or its distance changed.
    Fought,

    // The fish turned to another pull.
    Turned,

    // The fish turned to a run and leapt out of the water.
    Jumped,

    Caught,

    // A strike before any bite.
    LostEarly,

    // A strike at a nibble, which spooked the fish.
    LostNibble,

    // No strike, or one too late.
    LostLate,

    // Strained too hard: the line broke.
    LostSnapped,

    // Held too slack for too long: the fish threw the hook.
    LostSlack,

    // The fish ran out more line than there is.
    LostRunaway,

    // The rest is over: the angler may cast again.
    Ready,
}
