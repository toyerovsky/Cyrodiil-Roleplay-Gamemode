using ReadyM.Api.Idents;

namespace Roleplay.Jobs.Server;

/// <summary>
/// The Jobs foremen, for a mod that owns the way a player reaches them (a topic in the game's dialogue) and a window to
/// deal in: which foreman a person of the game is, what they offer a player, and doing a deal. Texts are whole English
/// sentences, ready to show.
/// </summary>
/// <remarks>Server thread only.</remarks>
public interface IJobForemen
{
    /// <summary>The base form every foreman is made from, which is what the game offers a dialogue topic by.</summary>
    uint NpcBase { get; }

    /// <summary>The id of the foreman standing under the ref id; null when the ref id is no foreman's.</summary>
    string? FindByNpc(uint npcRefId);

    /// <summary>What the foreman offers the player now; null when the foreman is gone or the player is in no world.</summary>
    ForemanWindow? WindowOf(PlayerId player, string foremanId);

    /// <summary>How far the player is from the foreman; null when the foreman is gone or in another area.</summary>
    float? DistanceTo(PlayerId player, string foremanId);

    /// <summary>
    /// Does a deal from the foreman's window: <see cref="ForemanDeals.PayId"/> collects the wages owed, a buy or sell
    /// offer's id buys or sells <paramref name="count"/> of it.
    /// </summary>
    ForemanDealResult Deal(PlayerId player, string foremanId, string offerId, int count);
}
