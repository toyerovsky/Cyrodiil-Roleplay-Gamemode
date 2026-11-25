namespace CyrodiilRP.Client.Fishing;

// What an angler's upper body plays: a hold that loops, or a move played once before the next pose.
internal enum AnglerPose
{
    // The rod held out over the water, looped.
    Holding,

    // The swing that throws the float, then Holding.
    Casting,

    // The jerk that sets the hook, then Fighting.
    Striking,

    // The rod braced against the fish, looped.
    Fighting,

    // The catch held up and looked at, then Holding.
    Showing,
}
