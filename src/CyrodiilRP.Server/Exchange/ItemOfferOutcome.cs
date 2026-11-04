namespace CyrodiilRP.Server.Exchange;

internal enum ItemOfferOutcome
{
    // The player no longer carries the stack, or it never was theirs.
    NotCarried,

    // Below none, or above what they carry.
    OutOfRange,

    // The offer already held that many.
    Unchanged,

    Changed,
}
