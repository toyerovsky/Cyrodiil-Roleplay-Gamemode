namespace CyrodiilRP.Client.Fishing;

// What the fish under an angler's float is drawn doing, from the server's FishMove.
internal enum FishSwim
{
    // Round the float, curious.
    Circling,

    // At the float and back, a nibble.
    Darting,

    // Down under the float with the bait.
    Diving,

    // On the line, under the float as it is dragged.
    Following,

    // Out of the water and back in.
    Jumping,

    // Off and away, then gone.
    Leaving,
}
