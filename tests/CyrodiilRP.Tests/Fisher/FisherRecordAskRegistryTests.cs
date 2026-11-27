using CyrodiilRP.Server.Fisher;
using CyrodiilRP.Server.Players;

namespace CyrodiilRP.Tests.Fisher;

public sealed class FisherRecordAskRegistryTests
{
    private const long Start = 10_000;
    private static readonly long Soonest = (long)RecordBoardRules.AskSoonest.TotalMilliseconds;

    [Fact]
    public void A_player_is_answered_again_only_once_the_soonest_time_has_passed_and_each_player_for_themselves()
    {
        var asks = new RecordAskRegistry();
        var first = CreatePlayer();
        var second = CreatePlayer();

        Assert.True(asks.TryAsk(first, Start));
        Assert.False(asks.TryAsk(first, Start + Soonest - 1));
        Assert.True(asks.TryAsk(second, Start + 1));
        Assert.True(asks.TryAsk(first, Start + Soonest));
        Assert.False(asks.TryAsk(first, Start + Soonest + 1));
    }

    [Fact]
    public void A_player_who_left_is_forgotten()
    {
        var asks = new RecordAskRegistry();
        var player = CreatePlayer();
        Assert.True(asks.TryAsk(player, Start));

        asks.Forget(player);

        Assert.True(asks.TryAsk(player, Start + 1));
    }

    private static OnlinePlayer CreatePlayer() => new(default, Guid.NewGuid(), DateTimeOffset.UnixEpoch) { IsLoaded = true };
}
