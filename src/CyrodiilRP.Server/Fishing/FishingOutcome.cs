namespace CyrodiilRP.Server.Fishing;

internal enum FishingOutcome
{
    Caught,
    Found,

    // Caught, held up and let go again.
    Released,
    LostEarly,
    LostNibble,
    LostLate,
    LostSnapped,
    LostSlack,
    LostRunaway,
}
