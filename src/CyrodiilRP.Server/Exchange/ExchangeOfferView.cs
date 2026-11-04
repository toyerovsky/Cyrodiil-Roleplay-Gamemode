namespace CyrodiilRP.Server.Exchange;

internal sealed record ExchangeOfferView(int Gold, IReadOnlyList<ExchangeItemView> Items, bool Confirmed);
