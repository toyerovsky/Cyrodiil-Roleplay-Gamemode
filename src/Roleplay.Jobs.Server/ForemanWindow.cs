namespace Roleplay.Jobs.Server;

/// <summary>
/// A foreman's offer to one player: who they are, the gold the player carries, the dock wages owed (when the foreman
/// pays them: UnpaidCrates at Wage gold each), and what they sell and buy.
/// </summary>
public sealed record ForemanWindow(string ForemanId, string Name, int Gold, bool PaysWages, int UnpaidCrates, int Wage,
    IReadOnlyList<ForemanOffer> Offers);
