namespace CyrodiilRP.Server.Exchange;

internal enum ConfirmOutcome
{
    // The offer changed since its player saw it.
    Changed,

    // Within the delay after the last change.
    TooSoon,

    // Confirmed, the other side has not yet.
    Waiting,

    Agreed,
}
