using CyrodiilRP.Data.Auction;
using CyrodiilRP.Server.Exchange;
using CyrodiilRP.Server.Traders;

namespace CyrodiilRP.Server.Auction;

// An auction house window a player has open. Lists: what the database last gave it, null before the first read. Reading:
// counts the reads, so only the latest one is shown. Seen: what the character carried when the window last showed it,
// null before it was first sent.
internal sealed record OpenAuction(TraderSpotRecord Spot, long CharacterId, AuctionQuery Query, AuctionWindow? Lists, int Reading, ExchangeHoldings? Seen);
