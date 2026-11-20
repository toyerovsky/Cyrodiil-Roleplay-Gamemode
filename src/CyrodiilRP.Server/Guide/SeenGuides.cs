using System.Collections.Immutable;
using CyrodiilRP.Data;
using CyrodiilRP.Data.Guides;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Players;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Guide;

// The guides each account playing now has seen, read once as it loads, so a window that opens never waits on the
// database for them.
[Service]
internal sealed partial class SeenGuides(OnlinePlayers online, Persistence persistence, Fire fire, SystemClock clock)
{
    private const string LoadJob = "guides.load";
    private const string MarkJob = "guides.mark";

    private readonly Dictionary<OnlinePlayer, ImmutableHashSet<GuideTopic>> _seen = [];

    private void Start()
    {
        online.Loaded += Load;
        online.Left += player => _seen.Remove(player);
    }

    // Null until the account's guides are read: meanwhile a window opens without its guide.
    public bool? Has(OnlinePlayer player, GuideTopic topic) => _seen.TryGetValue(player, out var seen) ? seen.Contains(topic) : null;

    // At once in memory, the row after it: a write that fails shows the guide again at the next join, nothing worse.
    public void Mark(OnlinePlayer player, GuideTopic topic)
    {
        if (!_seen.TryGetValue(player, out var seen) || seen.Contains(topic))
            return;

        _seen[player] = seen.Add(topic);
        var accountId = player.AccountId;
        var at = clock.UtcNow;
        fire.Run(MarkJob, () => persistence.Writer.EnqueueAsync(MarkJob, (db, ct) => GuideBook.MarkSeenAsync(db, accountId, topic, at, ct)));
    }

    private void Load(OnlinePlayer player) => fire.Run(LoadJob, async () =>
    {
        var accountId = player.AccountId;
        var topics = await persistence.Reader.RunAsync((db, ct) => GuideBook.ReadSeenAsync(db, accountId, ct));
        if (player.IsLive)
            _seen[player] = [.. topics];
    });
}
