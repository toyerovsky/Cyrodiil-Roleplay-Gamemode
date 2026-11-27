using CyrodiilRP.Server.Players;

namespace CyrodiilRP.Server.Fisher;

// When each player last asked for the records board: one who asks again within RecordBoardRules.AskSoonest is not
// answered, so a game cannot make the server build the board for it over and over.
internal sealed class RecordAskRegistry
{
    private readonly Dictionary<OnlinePlayer, long> _askedAtMs = [];

    // False for an ask too soon after the last one answered.
    public bool TryAsk(OnlinePlayer player, long now)
    {
        if (_askedAtMs.TryGetValue(player, out var askedAt) && now - askedAt < (long)RecordBoardRules.AskSoonest.TotalMilliseconds)
            return false;

        _askedAtMs[player] = now;
        return true;
    }

    public void Forget(OnlinePlayer player) => _askedAtMs.Remove(player);
}
