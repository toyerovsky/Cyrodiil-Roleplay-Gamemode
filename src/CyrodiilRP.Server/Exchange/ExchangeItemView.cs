namespace CyrodiilRP.Server.Exchange;

// Key: what the page names the stack by when it offers some. Used: its durability or charge have gone down.
internal sealed record ExchangeItemView(string Key, string Name, bool Used, int Count);
