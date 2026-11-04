using CyrodiilRP.Server.Admin;
using CyrodiilRP.Server.Exchange;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Players;
using ReadyM.Relay.Common.Oblivion.ECS.Values;

namespace CyrodiilRP.Tests.Exchange;

public sealed class ExchangeDealTests
{
    private const long OpenedAtMs = 1_000;
    private const int Offered = 50;
    private const int Carried = 5;
    private const float AsMade = InventoryEntry.NoInstanceOverride;

    private static readonly DateTimeOffset Now = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);
    private static readonly long DelayMs = (long)ExchangeRules.ConfirmDelay.TotalMilliseconds;
    private static readonly ExchangeItem Sword = new(new ExchangeStack(0x1000, AsMade, AsMade), new GameItem("WeapIronSword", 0x1000, "Iron Sword", "WEAP"), Carried);
    private static readonly ExchangeItem Bread = new(new ExchangeStack(0x2000, AsMade, AsMade), new GameItem("Breadloaf", 0x2000, "Bread Loaf", "INGR"), Carried);

    private readonly OnlinePlayer _asker = CreatePlayer();
    private readonly OnlinePlayer _asked = CreatePlayer();
    private readonly ExchangeDeal _deal;

    public ExchangeDealTests() => _deal = new ExchangeDeal(_asker, _asked, OpenedAtMs);

    private long Ready => _deal.ChangedAtMs + DelayMs;

    [Fact]
    public void Nobody_confirms_before_the_delay_after_the_opening()
    {
        Assert.Equal(ConfirmOutcome.TooSoon, _deal.Confirm(_asker, _deal.Version, OpenedAtMs + DelayMs - 1));
        Assert.Equal(ConfirmOutcome.Waiting, _deal.Confirm(_asker, _deal.Version, OpenedAtMs + DelayMs));
    }

    [Fact]
    public void A_trade_is_agreed_once_both_confirm_the_same_version()
    {
        Assert.Equal(ConfirmOutcome.Waiting, _deal.Confirm(_asker, _deal.Version, Ready));
        Assert.Equal(ConfirmOutcome.Agreed, _deal.Confirm(_asked, _deal.Version, Ready));
    }

    [Fact]
    public void A_change_takes_both_confirmations_back_and_starts_the_delay_again()
    {
        _deal.Confirm(_asked, _deal.Version, Ready);
        var seen = _deal.Version;
        var changedAt = Ready + 500;

        Assert.True(_deal.Offer(_asker, Offered, changedAt));

        Assert.False(_deal.Asked.Confirmed);
        Assert.Equal(seen + 1, _deal.Version);
        Assert.Equal(ConfirmOutcome.Changed, _deal.Confirm(_asked, seen, changedAt + DelayMs));
        Assert.Equal(ConfirmOutcome.TooSoon, _deal.Confirm(_asked, _deal.Version, changedAt + 1));
    }

    [Fact]
    public void Offering_the_same_amount_again_changes_nothing()
    {
        _deal.Offer(_asker, Offered, Ready);
        _deal.Confirm(_asked, _deal.Version, Ready + DelayMs);
        var version = _deal.Version;

        Assert.False(_deal.Offer(_asker, Offered, Ready + DelayMs));

        Assert.Equal(version, _deal.Version);
        Assert.True(_deal.Asked.Confirmed);
    }

    [Fact]
    public void An_offer_above_what_its_side_still_carries_shrinks_and_counts_as_a_change()
    {
        _deal.Offer(_asker, Offered, Ready);
        var version = _deal.Version;

        Assert.True(_deal.Trim(player => CreateHoldings(player == _asker ? Offered - 10 : 0), Ready));

        Assert.Equal(Offered - 10, _deal.Asker.Gold);
        Assert.Equal(version + 1, _deal.Version);
        Assert.False(_deal.Trim(_ => CreateHoldings(Offered), Ready));
    }

    [Fact]
    public void Offering_an_item_takes_both_confirmations_back_and_starts_the_delay_again()
    {
        _deal.Confirm(_asked, _deal.Version, Ready);
        var seen = _deal.Version;
        var changedAt = Ready + 500;

        Assert.Equal(ItemOfferOutcome.Changed, OfferSword(2, changedAt));

        Assert.False(_deal.Asked.Confirmed);
        Assert.Equal(seen + 1, _deal.Version);
        Assert.Equal(ConfirmOutcome.TooSoon, _deal.Confirm(_asked, _deal.Version, changedAt + 1));
    }

    [Fact]
    public void Offering_as_many_of_an_item_again_changes_nothing()
    {
        OfferSword(2, Ready);
        _deal.Confirm(_asked, _deal.Version, Ready + DelayMs);
        var version = _deal.Version;

        Assert.Equal(ItemOfferOutcome.Unchanged, OfferSword(2, Ready + DelayMs));

        Assert.Equal(version, _deal.Version);
        Assert.True(_deal.Asked.Confirmed);
    }

    [Fact]
    public void Offering_none_of_an_item_takes_it_out_of_the_offer()
    {
        OfferSword(2, Ready);

        Assert.Equal(ItemOfferOutcome.Changed, OfferSword(0, Ready));

        Assert.Empty(_deal.Asker.Items);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(Carried + 1)]
    public void An_item_offer_outside_what_the_player_carries_changes_nothing(int count)
    {
        var version = _deal.Version;

        Assert.Equal(ItemOfferOutcome.OutOfRange, OfferSword(count, Ready));

        Assert.Empty(_deal.Asker.Items);
        Assert.Equal(version, _deal.Version);
    }

    [Fact]
    public void A_stack_the_player_does_not_carry_cannot_be_offered()
    {
        var outcome = _deal.OfferItem(_asker, CreateHoldings(0, Bread), Sword.Stack.Key, 1, Ready);

        Assert.Equal(ItemOfferOutcome.NotCarried, outcome);
        Assert.Empty(_deal.Asker.Items);
    }

    [Fact]
    public void An_offered_item_shrinks_to_what_is_left_and_counts_as_a_change()
    {
        OfferSword(Carried, Ready);
        var version = _deal.Version;

        Assert.True(_deal.Trim(_ => CreateHoldings(0, Sword with { Count = 2 }), Ready));

        Assert.Equal(2, Assert.Single(_deal.Asker.Items).Count);
        Assert.Equal(version + 1, _deal.Version);
    }

    [Fact]
    public void An_offered_item_no_longer_carried_leaves_the_offer()
    {
        OfferSword(1, Ready);

        Assert.True(_deal.Trim(_ => CreateHoldings(0), Ready));

        Assert.Empty(_deal.Asker.Items);
    }

    [Fact]
    public void An_offer_still_carried_stands()
    {
        OfferSword(2, Ready);
        var version = _deal.Version;

        Assert.False(_deal.Trim(_ => CreateHoldings(0, Sword), Ready));

        Assert.Equal(version, _deal.Version);
    }

    [Fact]
    public void Each_side_hands_over_its_items_and_the_gold_moves_once()
    {
        _deal.Offer(_asker, Offered, Ready);
        _deal.Offer(_asked, 20, Ready);
        OfferSword(2, Ready);
        _deal.OfferItem(_asked, CreateHoldings(0, Bread), Bread.Stack.Key, 3, Ready);

        Assert.Equal(
            [
                new ExchangeMove(_asker, _asked, Sword.Stack, 2),
                new ExchangeMove(_asked, _asker, Bread.Stack, 3),
                new ExchangeMove(_asker, _asked, ExchangeStack.Gold, Offered - 20),
            ],
            _deal.ListMoves());
    }

    [Fact]
    public void The_gold_moves_towards_the_side_that_offered_less()
    {
        _deal.Offer(_asked, Offered, Ready);

        Assert.Equal([new ExchangeMove(_asked, _asker, ExchangeStack.Gold, Offered)], _deal.ListMoves());
    }

    [Fact]
    public void Equal_gold_and_no_items_move_nothing()
    {
        _deal.Offer(_asker, Offered, Ready);
        _deal.Offer(_asked, Offered, Ready);

        Assert.Empty(_deal.ListMoves());
        Assert.Empty(_deal.ListResults(_asker));
    }

    [Fact]
    public void Each_side_reads_the_gold_and_the_pieces_it_got_and_gave()
    {
        _deal.Offer(_asker, Offered, Ready);
        OfferSword(2, Ready);
        _deal.OfferItem(_asked, CreateHoldings(0, Bread), Bread.Stack.Key, 3, Ready);

        var itemsReceived = ExchangeText.PartItemsReceived(0).Key;
        var itemsGave = ExchangeText.PartItemsGave(0).Key;
        Assert.Equal([ExchangeText.PartGoldGave(0).Key, itemsReceived, itemsGave], _deal.ListResults(_asker).Select(part => part.Key));
        Assert.Equal([ExchangeText.PartGoldReceived(0).Key, itemsReceived, itemsGave], _deal.ListResults(_asked).Select(part => part.Key));
        Assert.Equal(3, _deal.ListResults(_asker)[1].Args.Single().Value);
        Assert.Equal(2, _deal.ListResults(_asker)[2].Args.Single().Value);
    }

    [Fact]
    public void The_audit_names_the_items_by_their_editor_ids_and_nothing_when_none_went()
    {
        OfferSword(2, Ready);
        _deal.OfferItem(_asker, CreateHoldings(0, Bread), Bread.Stack.Key, 1, Ready);

        Assert.Equal("Breadloaf x1; WeapIronSword x2", _deal.Asker.DescribeItems());
        Assert.Null(_deal.Asked.DescribeItems());
    }

    [Fact]
    public void The_gold_moves_once_by_the_difference_of_the_offers()
    {
        _deal.Offer(_asker, Offered, Ready);
        _deal.Offer(_asked, 20, Ready);
        Assert.Equal(Offered - 20, _deal.Net);

        _deal.Offer(_asked, 80, Ready);
        Assert.Equal(Offered - 80, _deal.Net);
    }

    private static OnlinePlayer CreatePlayer() => new(default, Guid.NewGuid(), Now) { IsLoaded = true };

    private static ExchangeHoldings CreateHoldings(int gold, params ExchangeItem[] items) => new(gold, items);

    private ItemOfferOutcome OfferSword(int count, long nowMs) => _deal.OfferItem(_asker, CreateHoldings(0, Sword), Sword.Stack.Key, count, nowMs);
}
