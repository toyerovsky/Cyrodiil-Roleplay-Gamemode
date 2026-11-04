namespace CyrodiilRP.Server.Exchange;

// A trade as one side sees it, sent at every change. ConfirmInMs: how long until a confirmation is taken. Items: what
// the player can offer, with all they carry of each.
internal sealed record ExchangeView(
    int Version,
    string Other,
    int Carried,
    int Max,
    long ConfirmInMs,
    IReadOnlyList<ExchangeItemView> Items,
    ExchangeOfferView Mine,
    ExchangeOfferView Theirs);
