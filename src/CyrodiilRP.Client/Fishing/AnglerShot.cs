namespace CyrodiilRP.Client.Fishing;

// What the angler's own camera shows at a moment of the fishing.
internal enum AnglerShot
{
    // Over the right shoulder at the aim or the float.
    Shoulder,

    // Closer and lower, on the float as it goes under.
    Bite,

    // Over the shoulder turned after the dragged float.
    Fight,

    // From the front right, on the catch in the hand.
    Catch,
}
