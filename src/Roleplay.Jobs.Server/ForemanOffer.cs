namespace Roleplay.Jobs.Server;

/// <summary>
/// One thing a foreman sells to the player or buys from them, at Price gold a piece. Icon: the game's editor id whose
/// icon stands for it, empty for none. Carried: how many the player has, for something the foreman buys.
/// </summary>
public sealed record ForemanOffer(string Id, ForemanOfferKind Kind, string Name, string Icon, int Price, int Carried);
