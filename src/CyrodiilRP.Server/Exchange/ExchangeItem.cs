using CyrodiilRP.Server.Admin;

namespace CyrodiilRP.Server.Exchange;

// Copies of one stack: those a player carries, or those they offer.
internal sealed record ExchangeItem(ExchangeStack Stack, GameItem Item, int Count);
