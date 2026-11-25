namespace CyrodiilRP.Client.Fishing;

// How a line ended, as the server's FishingView names it.
internal enum LineOutcome
{
    Caught,
    Found,
    LostEarly,
    LostNibble,
    LostLate,
    LostSnapped,
    LostSlack,
    LostRunaway,
}
