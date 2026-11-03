using CyrodiilRP.Data.Characters;
using CyrodiilRP.Server.Characters;
using CyrodiilRP.Server.Players;
using CyrodiilRP.Server.Welcome;

namespace CyrodiilRP.Tests.Welcome;

public sealed class WelcomeCardsTests
{
    private static readonly DateTimeOffset Created = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset LastPlayed = new(2026, 10, 25, 18, 30, 0, TimeSpan.Zero);
    private const long PlayedSeconds = 7_530;

    [Fact]
    public void A_returning_player_sees_their_character_and_the_locked_slot_but_no_empty_one()
    {
        var player = CreatePlayer(previousVisit: LastPlayed);
        player.Character = CharacterRecord.Create(new CharacterSnapshot(7, "Marcus", "Varro", Created, LastPlayed, PlayedSeconds), []);

        var view = WelcomeCards.Build(player);

        Assert.False(view.FirstVisit);
        Assert.Equal("toyer", view.Nickname);
        var character = Assert.Single(view.Characters);
        Assert.Equal("Marcus", character.FirstName);
        Assert.Equal("Varro", character.LastName);
        Assert.Equal(LastPlayed.ToUnixTimeMilliseconds(), character.LastPlayedAt);
        Assert.Equal(PlayedSeconds, character.PlayedSeconds);
        Assert.Equal(0, view.EmptySlots);
        Assert.Equal(CharacterSlots.Locked, view.LockedSlots);
    }

    [Fact]
    public void A_first_visit_has_no_character_and_the_open_slot_empty()
    {
        var view = WelcomeCards.Build(CreatePlayer(previousVisit: null));

        Assert.True(view.FirstVisit);
        Assert.Empty(view.Characters);
        Assert.Equal(CharacterSlots.Open, view.EmptySlots);
        Assert.Equal(CharacterSlots.Locked, view.LockedSlots);
    }

    [Fact]
    public void A_returning_player_without_a_character_is_not_a_first_visit()
    {
        var view = WelcomeCards.Build(CreatePlayer(previousVisit: LastPlayed));

        Assert.False(view.FirstVisit);
        Assert.Equal(CharacterSlots.Open, view.EmptySlots);
    }

    private static OnlinePlayer CreatePlayer(DateTimeOffset? previousVisit) =>
        new(default, Guid.NewGuid(), Created) { IsLoaded = true, AccountName = "toyer", PreviousVisitAt = previousVisit };
}
