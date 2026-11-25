using CyrodiilRP.Server.Players;

namespace CyrodiilRP.Server.Interactions;

// Who the wheel over the one in front can pick: players in the world as their characters, whom the others see. One
// still at the welcome or making a character is no one's to trade with, invite or act on, and nobody finds a hidden one.
internal static class WheelTargets
{
    public static IEnumerable<OnlinePlayer> Filter(IEnumerable<OnlinePlayer> players, Func<OnlinePlayer, bool> isHidden) =>
        players.Where(p => p.IsPlaying && !isHidden(p));
}
