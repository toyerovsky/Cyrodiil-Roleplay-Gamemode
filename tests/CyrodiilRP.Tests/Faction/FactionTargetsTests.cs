using CyrodiilRP.Server.Faction;

namespace CyrodiilRP.Tests.Faction;

public sealed class FactionTargetsTests
{
    private const ushort BurzNumber = 12;

    private readonly Guild _guild = new();

    [Fact]
    public void A_rank_is_named_by_its_number_after_the_mark()
    {
        var rank = FactionTargets.TakeRank(_guild.Faction, $"{FactionTargets.NumberMark}{_guild.Warder.Number} kick on", out var rest);

        Assert.Equal(_guild.Warder, rank);
        Assert.Equal("kick on", rest);
    }

    [Fact]
    public void The_longest_name_the_text_starts_with_is_the_one_meant()
    {
        var senior = _guild.AddRank("Warder of the North");

        Assert.Equal(senior, FactionTargets.TakeRank(_guild.Faction, "warder of the north invite", out var rest));
        Assert.Equal("invite", rest);

        Assert.Equal(_guild.Warder, FactionTargets.TakeRank(_guild.Faction, "Warder of the South", out rest));
        Assert.Equal("of the South", rest);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Warders")]
    [InlineData("#99")]
    [InlineData("#Warder")]
    [InlineData("Champion")]
    public void A_text_that_names_no_rank_finds_none(string text) =>
        Assert.Null(FactionTargets.TakeRank(_guild.Faction, text, out _));

    [Fact]
    public void A_member_is_named_by_a_player_number_by_the_mark_or_by_the_full_name()
    {
        long? FindCharacter(ushort number) => number == BurzNumber ? Guild.OfficerId : null;

        Assert.Equal(_guild.Officer, FactionTargets.TakeMember(_guild.Faction, FindCharacter, $"{BurzNumber} Associate", out var rest));
        Assert.Equal("Associate", rest);
        Assert.Equal(_guild.Member, FactionTargets.TakeMember(_guild.Faction, FindCharacter, $"#{Guild.MemberId}", out rest));
        Assert.Equal(string.Empty, rest);
        Assert.Equal(_guild.Member, FactionTargets.TakeMember(_guild.Faction, FindCharacter, "lum gro-baroth Warder", out rest));
        Assert.Equal("Warder", rest);
    }

    [Fact]
    public void A_player_outside_the_faction_and_a_number_nobody_holds_name_no_member()
    {
        const ushort Stranger = 5;
        long? FindCharacter(ushort number) => number == Stranger ? 999 : null;

        Assert.Null(FactionTargets.TakeMember(_guild.Faction, FindCharacter, $"{Stranger}", out _));
        Assert.Null(FactionTargets.TakeMember(_guild.Faction, FindCharacter, "77", out _));
        Assert.Null(FactionTargets.TakeMember(_guild.Faction, FindCharacter, "Lum", out _));
    }
}
