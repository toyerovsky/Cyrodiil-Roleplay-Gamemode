using CyrodiilRP.Server.Characters;
using CyrodiilRP.Server.Players;

namespace CyrodiilRP.Tests.Players;

public sealed class PlayerNamesTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;

    [Theory]
    [InlineData("Marcus Varro got a minute?")]
    [InlineData("marcus varro got a minute?")]
    [InlineData("Marcus got a minute?")]
    [InlineData("kral got a minute?")]
    public void A_player_is_found_by_full_name_first_name_or_nick(string text)
    {
        var marcus = CreatePlayer("kral", "Marcus", "Varro");
        var other = CreatePlayer("pete", "Jim", "Dasteroid");

        var named = PlayerNames.Take(text, [other, marcus]);

        Assert.Equal(PlayerNameMatch.Found, named.Match);
        Assert.Same(marcus, named.Player);
        Assert.Equal("got a minute?", named.Rest);
    }

    [Fact]
    public void The_longest_name_wins_over_a_first_name_another_player_shares()
    {
        var marcusVarro = CreatePlayer("kral", "Marcus", "Varro");
        var marcusAntonius = CreatePlayer("pete", "Marcus", "Antonius");

        var named = PlayerNames.Take("Marcus Antonius hi", [marcusVarro, marcusAntonius]);

        Assert.Same(marcusAntonius, named.Player);
        Assert.Equal("hi", named.Rest);
    }

    [Fact]
    public void A_shared_first_name_alone_is_ambiguous()
    {
        var players = new[] { CreatePlayer("kral", "Marcus", "Varro"), CreatePlayer("pete", "Marcus", "Antonius") };

        var named = PlayerNames.Take("Marcus hi", players);

        Assert.Equal(PlayerNameMatch.Ambiguous, named.Match);
        Assert.Null(named.Player);
    }

    [Theory]
    [InlineData("Marc hi")]
    [InlineData("Marcusx hi")]
    [InlineData("")]
    public void A_name_must_match_whole_words(string text)
    {
        var named = PlayerNames.Take(text, [CreatePlayer("kral", "Marcus", "Varro")]);

        Assert.Equal(PlayerNameMatch.Unknown, named.Match);
        Assert.Null(named.Player);
    }

    [Fact]
    public void A_player_without_a_character_is_found_by_nick_and_an_empty_nick_matches_nothing()
    {
        var newcomer = new OnlinePlayer(default, Guid.NewGuid(), Now) { AccountName = "pete" };
        var unnamed = new OnlinePlayer(default, Guid.NewGuid(), Now);

        Assert.Same(newcomer, PlayerNames.Take("pete hello", [unnamed, newcomer]).Player);
        Assert.Equal(PlayerNameMatch.Unknown, PlayerNames.Take("hello", [unnamed]).Match);
    }

    private static OnlinePlayer CreatePlayer(string nick, string firstName, string lastName) =>
        new(default, Guid.NewGuid(), Now)
        {
            AccountName = nick,
            Character = new CharacterRecord(1, firstName, lastName, Now, Now, 0),
        };
}
