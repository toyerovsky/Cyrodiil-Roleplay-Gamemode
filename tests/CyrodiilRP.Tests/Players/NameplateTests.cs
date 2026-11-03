using CyrodiilRP.Common.Players;

namespace CyrodiilRP.Tests.Players;

public sealed class NameplateTests
{
    private const string Name = "Marius Varro";

    [Fact]
    public void A_name_after_the_staff_mark_is_on_duty_and_a_name_alone_is_not()
    {
        Assert.True(Nameplate.IsOnDuty(Nameplate.DutyMark + Name));
        Assert.False(Nameplate.IsOnDuty(Name));
        Assert.False(Nameplate.IsOnDuty("Staff " + Name));
    }
}
