namespace CyrodiilRP.Server.Bank;

// The bank's window, sent as it opens and again as the balance or the carried gold changes.
internal sealed record BankView(long Balance, int Gold);
