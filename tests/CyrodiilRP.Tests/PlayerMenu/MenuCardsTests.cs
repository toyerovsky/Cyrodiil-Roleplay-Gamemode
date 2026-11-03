using CyrodiilRP.Server.Characters;
using CyrodiilRP.Server.Conditions;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.PlayerMenu;
using CyrodiilRP.Server.Players;

namespace CyrodiilRP.Tests.PlayerMenu;

public sealed class MenuCardsTests
{
    private const long WrittenSeconds = 3600;
    private const int Gold = 42;

    private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);
    private static readonly Phrase Muted = new("test.muted");
    private static readonly Phrase Bound = new("test.bound");

    [Fact]
    public void The_time_played_counts_what_is_not_written_yet()
    {
        var player = CreatePlayer();
        player.PlayCountedUntil = Now.AddSeconds(-45);

        var card = MenuCards.BuildCharacter(player, CreateCharacter(), Gold, Now);

        Assert.Equal(WrittenSeconds + 45, card.PlayedSeconds);
        Assert.Equal(Gold, card.Gold);
        Assert.Equal(Now.AddDays(-3).ToUnixTimeMilliseconds(), card.CreatedAt);
    }

    [Fact]
    public void The_account_names_the_nickname_and_its_visit_before_this_one()
    {
        var player = CreatePlayer();
        player.AccountName = "Toyer";
        player.AccountCreatedAt = Now.AddDays(-30);
        player.PreviousVisitAt = Now.AddDays(-1);

        var card = MenuCards.BuildAccount(player, [], phrase => phrase.Key);

        Assert.Equal("Toyer", card.Nickname);
        Assert.Equal(Now.AddDays(-30).ToUnixTimeMilliseconds(), card.CreatedAt);
        Assert.Equal(Now.AddDays(-1).ToUnixTimeMilliseconds(), card.PreviousVisitAt);
        Assert.Empty(card.Sanctions);
    }

    [Fact]
    public void A_first_visit_has_none_before_it()
    {
        Assert.Null(MenuCards.BuildAccount(CreatePlayer(), [], phrase => phrase.Key).PreviousVisitAt);
    }

    [Fact]
    public void Sanctions_come_in_the_readers_words_soonest_over_first_and_those_until_lifted_last()
    {
        TitledCondition[] conditions =
        [
            new(Bound, new ActiveCondition("test.bound", Now, null, null, "until lifted")),
            new(Muted, new ActiveCondition("test.muted", Now, Now.AddMinutes(10), null, "spam")),
        ];

        var card = MenuCards.BuildAccount(CreatePlayer(), conditions, phrase => $"[{phrase.Key}]");

        Assert.Equal(["[test.muted]", "[test.bound]"], card.Sanctions.Select(sanction => sanction.Title));
        Assert.Equal("spam", card.Sanctions[0].Reason);
        Assert.Equal(Now.AddMinutes(10).ToUnixTimeMilliseconds(), card.Sanctions[0].EndsAt);
        Assert.Null(card.Sanctions[1].EndsAt);
    }

    private static OnlinePlayer CreatePlayer() => new(default, Guid.NewGuid(), Now) { IsLoaded = true };

    private static CharacterRecord CreateCharacter() => new(1, "Marcus", "Varro", Now.AddDays(-3), Now, WrittenSeconds);
}
