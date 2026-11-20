namespace CyrodiilRP.Server.Auction;

// As the page sent it. Item: the stack's key in the window. A price of 0 is none.
internal sealed record AuctionPostRequest(string Item, int Count, int StartPrice, int BuyoutPrice, int Hours);
