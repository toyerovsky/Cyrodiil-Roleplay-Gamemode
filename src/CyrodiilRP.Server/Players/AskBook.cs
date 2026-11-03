namespace CyrodiilRP.Server.Players;

// In memory, the asks waiting for an answer, the oldest first.
internal sealed class AskBook
{
    private readonly List<OpenAsk> _open = [];

    private int _lastId;

    public OpenAsk Open(PlayerAsk ask, string kind, long expiresAtMs, AskAnswers answers)
    {
        var open = new OpenAsk(++_lastId, ask, kind, expiresAtMs, answers);
        _open.Add(open);
        return open;
    }

    // False when it was not open any more.
    public bool Close(PlayerAsk ask) => _open.RemoveAll(open => open.Ask == ask) > 0;

    // Only the one asked answers, and only an ask still open: one that ran out as the key went down is no other's yes.
    public OpenAsk? FindAnswerable(OnlinePlayer player, int id) =>
        _open.FirstOrDefault(open => open.Id == id && open.Ask.Asked == player);

    public AsksView BuildView(OnlinePlayer reader, long nowMs, long totalMs)
    {
        var asks = _open
            .Where(open => open.Ask.Asked == reader || open.Ask.Asker == reader)
            .Select(open =>
            {
                var incoming = open.Ask.Asked == reader;
                var other = incoming ? open.Ask.Asker : open.Ask.Asked;
                return new AskView(open.Id, open.Kind, other.Name, incoming, Math.Max(0, open.ExpiresAtMs - nowMs), totalMs);
            })
            .ToList();
        return new AsksView(asks, asks.FirstOrDefault(ask => ask.Incoming)?.Id ?? 0);
    }
}
