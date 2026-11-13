using ReadyM.Api.Idents;

namespace Roleplay.Jobs.Server;

/// <summary>
/// The Jobs foremen, for a mod that owns the interaction key and a window to deal in: which foreman is in front of a
/// player, what they offer that player, and doing a deal. Texts are whole English sentences, ready to show.
/// </summary>
/// <remarks>Server thread only.</remarks>
public interface IJobForemen
{
    /// <summary>How far the nearest foreman in front of the player is, within <paramref name="reach"/> centimeters and a
    /// cone of <paramref name="coneDegrees"/> around where they face; null for none.</summary>
    float? MeasureInFront(PlayerId player, float reach, float coneDegrees);

    /// <summary>The id of the nearest foreman in front of the player; null for none.</summary>
    string? FindInFront(PlayerId player, float reach, float coneDegrees);

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
