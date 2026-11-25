namespace CyrodiilRP.Common.Fishing;

// What a hooked or curious fish does, as every game near the angler draws it.
public enum FishMove
{
    // A fish shows near the float and circles it. The point is the float's.
    Approach,

    // It darts at the float.
    Nibble,

    // It takes the bait and dives.
    Bite,

    // It is on the line: from now on it follows the float under the water.
    Hooked,

    // It leaps out of the water at the point, heading the yaw.
    Jump,

    // It is lifted to the angler's hand, sized by its length.
    Landed,

    // It swims off from the point, heading the yaw.
    Escaped,

    // It is taken away at once.
    Gone,
}
