using CyrodiilRP.Server.Fisher;
using CyrodiilRP.Server.Lore;

namespace CyrodiilRP.Tests.Fisher;

public sealed class FisherRecordNamesTests
{
    private const string Name = "Marius Caro";
    private const string Race = "Imperial";

    private static readonly DateTimeOffset Day = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_holder_in_the_game_is_named_as_the_reader_sees_them_everywhere(bool namePublic)
    {
        Assert.Equal(Race, RecordNames.Show(Holder(namePublic), shownInPlay: Race));
        Assert.Equal(Name, RecordNames.Show(Holder(namePublic), shownInPlay: Name));
    }

    [Fact]
    public void A_holder_away_is_named_only_when_the_name_is_public()
    {
        Assert.Equal(Name, RecordNames.Show(Holder(namePublic: true), shownInPlay: null));
    }

    [Fact]
    public void A_holder_away_with_a_private_name_is_a_stranger_of_no_known_race()
    {
        var shown = RecordNames.Show(Holder(namePublic: false), shownInPlay: null);

        Assert.Equal(GameRaces.StrangerLabel, shown);
        Assert.DoesNotContain("Marius", shown);
    }

    // What ShownNames gives a reader for a holder in the game, which Show hands on: the name to those who know it or
    // when it is public, the race to a stranger.
    [Theory]
    [InlineData(true, Name)]
    [InlineData(false, Race)]
    public void In_the_game_a_stranger_reads_the_race_and_an_acquaintance_the_name(bool knowsName, string expected)
    {
        var shownInPlay = ShownNames.Pick(hasCharacter: true, knowsName, Name, () => Race);

        Assert.Equal(expected, RecordNames.Show(Holder(namePublic: false), shownInPlay));
    }

    private static RecordHolder Holder(bool namePublic) => new(1, Name, namePublic, LengthCm: 74, Grams: 3400, Day);
}
