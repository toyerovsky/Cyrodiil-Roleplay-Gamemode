namespace Roleplay.Jobs.Server;

/// <summary>Which way an offer goes, from the player's side.</summary>
public enum ForemanOfferKind
{
    /// <summary>The player buys it from the foreman.</summary>
    Buy,

    /// <summary>The player sells it to the foreman.</summary>
    Sell,
}
