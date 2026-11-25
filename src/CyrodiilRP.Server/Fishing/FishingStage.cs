namespace CyrodiilRP.Server.Fishing;

internal enum FishingStage
{
    // The line is in: the angler picks where to cast.
    Aim,

    // The float lies on the water, nibbles come.
    Waiting,

    // A fish took the bait: the strike's window is open.
    Bite,

    // The fish is on the hook, held by the rod's bend.
    Landing,

    // After a catch or a loss, before the next cast.
    Rest,
}
