using CyrodiilRP.Server.Infrastructure;

namespace CyrodiilRP.Server.Players;

// One player asking another, to trade or to join a party, until the one asked answers or the ask runs out.
internal sealed class PlayerAsk(OnlinePlayer asker, OnlinePlayer asked)
{
    public OnlinePlayer Asker { get; } = asker;

    public OnlinePlayer Asked { get; } = asked;

    public TimerHandle? Timeout { get; set; }
}
