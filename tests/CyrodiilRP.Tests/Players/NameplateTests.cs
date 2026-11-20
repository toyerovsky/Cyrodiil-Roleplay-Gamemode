using CyrodiilRP.Common.Players;

namespace CyrodiilRP.Tests.Players;

public sealed class NameplateTests
{
    [Theory]
    [InlineData(false, false, false, "Lucia Gallus")]
    [InlineData(true, false, false, "[Staff] Lucia Gallus")]
    [InlineData(false, true, false, "[AFK] Lucia Gallus")]
    [InlineData(false, false, true, "Lucia Gallus ...")]
    [InlineData(true, true, true, "[Staff] [AFK] Lucia Gallus ...")]
    public void A_name_carries_the_marks_of_duty_away_and_typing(bool onDuty, bool away, bool typing, string expected) =>
        Assert.Equal(expected, Nameplate.Compose(onDuty, away, typing, "Lucia Gallus"));

    [Fact]
    public void The_client_reads_duty_and_away_back_from_the_name()
    {
        Assert.True(Nameplate.IsOnDuty(Nameplate.Compose(true, true, false, "Ra'jirr")));
        Assert.True(Nameplate.IsAway(Nameplate.Compose(true, true, false, "Ra'jirr")));
        Assert.True(Nameplate.IsAway(Nameplate.Compose(false, true, true, "Ra'jirr")));
        Assert.False(Nameplate.IsAway(Nameplate.Compose(true, false, true, "Ra'jirr")));
        Assert.False(Nameplate.IsOnDuty("Imperial"));
    }
}
