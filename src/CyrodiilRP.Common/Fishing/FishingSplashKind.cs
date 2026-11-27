namespace CyrodiilRP.Common.Fishing;

// How the water splashes at a float, each with a sight and a sound of its own in every game near.
public enum FishingSplashKind
{
    // The float lands: each game plays it itself when the cast's flight ends.
    Cast,

    // A fish tests the bait.
    Nibble,

    // The angler tugs the bait and the float hops.
    Twitch,

    // A fish takes the bait.
    Bite,

    // The fish runs on the line.
    Thrash,

    // The fish leaps out of the water.
    Jump,

    // The catch comes out of the water.
    Catch,

    // The line snaps.
    Snap,

    // A catch let go drops back into the water.
    Release,
}
