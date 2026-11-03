using CyrodiilRP.Server.Players;

namespace CyrodiilRP.Server.Interactions;

// Who the wheel over the one in front can pick: players in the world as their characters. One still at the welcome or
// making a character is no one's to trade with, invite or act on.
internal static class WheelTargets
{
    public static IEnumerable<OnlinePlayer> Filter(IEnumerable<OnlinePlayer> players) => players.Where(p => p.IsPlaying);
}
