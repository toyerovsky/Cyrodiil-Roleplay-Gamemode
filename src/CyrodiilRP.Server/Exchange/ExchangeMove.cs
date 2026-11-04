using CyrodiilRP.Server.Players;

namespace CyrodiilRP.Server.Exchange;

internal sealed record ExchangeMove(OnlinePlayer From, OnlinePlayer To, ExchangeStack Stack, int Count)
{
    public ExchangeMove Reversed => this with { From = To, To = From };
}
