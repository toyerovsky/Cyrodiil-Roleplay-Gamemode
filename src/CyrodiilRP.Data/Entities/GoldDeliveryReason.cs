namespace CyrodiilRP.Data;

// Stored by name.
public enum GoldDeliveryReason
{
    StartingGold,
    Gambling,

    // What the auction house took for a request it refused, when the character could not take it back at once.
    Auction,
}
