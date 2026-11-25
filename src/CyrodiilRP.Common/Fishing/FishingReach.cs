namespace CyrodiilRP.Common.Fishing;

// How far a rod reaches: the player's game aims within it, the server checks the cast against it. Centimetres and degrees.
public static class FishingReach
{
    // The rod and its line reach so far, a short swing no less.
    public const float Farthest = 450f;

    public const float Nearest = 150f;

    // How far to either side of the way the character faces a cast may turn.
    public const float AimDegrees = 60f;

    // The cast lands on the angler's water or on water whose surface is no farther above or below it.
    public const float SameWater = 150f;

    // Water at least this deep counts: a puddle does not.
    public const float ShallowestWater = 10f;

    // How much farther than the rod reaches the server lets a cast land, for where the character stands as the server
    // last heard it.
    public const float Slack = 100f;
}
