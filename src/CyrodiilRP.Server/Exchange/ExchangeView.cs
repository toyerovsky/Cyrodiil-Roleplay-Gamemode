namespace CyrodiilRP.Server.Exchange;

// A trade as one side sees it, sent at every change. ConfirmInMs: how long until a confirmation is taken.
internal sealed record ExchangeView(
    int Version,
    string Other,
    int Carried,
    int Max,
    long ConfirmInMs,
    ExchangeOfferView Mine,
    ExchangeOfferView Theirs);
