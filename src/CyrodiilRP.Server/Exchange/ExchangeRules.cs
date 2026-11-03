namespace CyrodiilRP.Server.Exchange;

// The texts show these numbers.
internal static class ExchangeRules
{
    public const int WalkAwayMetres = 5;

    public const float WalkAway = WalkAwayMetres * CentimetresPerMetre;

    // After every change of an offer, so nobody confirms what they had no time to see.
    public static readonly TimeSpan ConfirmDelay = TimeSpan.FromSeconds(3);

    // The window says the trade is done this long before it goes.
    public static readonly TimeSpan DoneShows = TimeSpan.FromSeconds(2);

    private const float CentimetresPerMetre = 100f;
}
