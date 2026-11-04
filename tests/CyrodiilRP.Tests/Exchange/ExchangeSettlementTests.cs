using CyrodiilRP.Server.Exchange;
using CyrodiilRP.Server.Players;
using CyrodiilRP.Tests.Fakes;
using ReadyM.Relay.Common.Oblivion.ECS.Values;

namespace CyrodiilRP.Tests.Exchange;

public sealed class ExchangeSettlementTests
{
    private const float AsMade = InventoryEntry.NoInstanceOverride;

    private static readonly ExchangeStack Sword = new(0x1000, AsMade, AsMade);
    private static readonly ExchangeStack Bread = new(0x2000, AsMade, AsMade);

    private readonly OnlinePlayer _marius = CreatePlayer();
    private readonly OnlinePlayer _lucia = CreatePlayer();
    private readonly ListLogger _logger = new();
    private readonly List<ExchangeMove> _tried = [];

    private ExchangeMove SwordMove => new(_marius, _lucia, Sword, 1);

    private ExchangeMove BreadMove => new(_lucia, _marius, Bread, 3);

    private ExchangeMove GoldMove => new(_marius, _lucia, ExchangeStack.Gold, 50);

    [Fact]
    public void Every_move_runs_in_turn_when_none_fails()
    {
        var settled = CreateSettlement(failing: []).TryRun([SwordMove, BreadMove, GoldMove]);

        Assert.True(settled);
        Assert.Equal([SwordMove, BreadMove, GoldMove], _tried);
        Assert.Empty(_logger.Errors);
    }

    [Fact]
    public void A_failed_move_takes_back_those_before_it_last_first_and_says_so()
    {
        var settled = CreateSettlement(failing: [GoldMove]).TryRun([SwordMove, BreadMove, GoldMove]);

        Assert.False(settled);
        Assert.Equal([SwordMove, BreadMove, GoldMove, BreadMove.Reversed, SwordMove.Reversed], _tried);
        Assert.Single(_logger.Errors);
    }

    [Fact]
    public void Nothing_after_the_failed_move_runs()
    {
        CreateSettlement(failing: [SwordMove]).TryRun([SwordMove, BreadMove]);

        Assert.Equal([SwordMove], _tried);
    }

    [Fact]
    public void A_move_that_cannot_be_taken_back_is_logged_and_the_others_still_go_back()
    {
        var settled = CreateSettlement(failing: [GoldMove, BreadMove.Reversed]).TryRun([SwordMove, BreadMove, GoldMove]);

        Assert.False(settled);
        Assert.Contains(SwordMove.Reversed, _tried);
        Assert.Equal(2, _logger.Errors.Count());
    }

    private static OnlinePlayer CreatePlayer() => new(default, Guid.NewGuid(), DateTimeOffset.UnixEpoch) { IsLoaded = true };

    private ExchangeSettlement CreateSettlement(IReadOnlyList<ExchangeMove> failing) =>
        new(
            move =>
            {
                _tried.Add(move);
                return !failing.Contains(move);
            },
            _logger);
}
