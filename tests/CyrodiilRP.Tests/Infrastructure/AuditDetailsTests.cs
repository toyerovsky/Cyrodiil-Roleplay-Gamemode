using CyrodiilRP.Server.Infrastructure;

namespace CyrodiilRP.Tests.Infrastructure;

public sealed class AuditDetailsTests
{
    [Fact]
    public void A_record_is_written_as_names_and_values_in_its_own_order()
    {
        Assert.Equal("minutes=30, reason=spam", AuditDetails.Format(new BanDetails(30, "spam")));
    }

    [Fact]
    public void An_empty_value_is_left_out()
    {
        Assert.Equal("minutes=0", AuditDetails.Format(new BanDetails(0, null)));
    }

    [Fact]
    public void Numbers_are_written_the_same_whatever_the_culture()
    {
        var culture = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("pl-PL");
            Assert.Equal("share=1.5", AuditDetails.Format(new ShareDetails(1.5m)));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = culture;
        }
    }

    [Fact]
    public void Text_stays_as_it_is_and_nothing_is_nothing()
    {
        Assert.Equal("plain words", AuditDetails.Format("plain words"));
        Assert.Null(AuditDetails.Format(null));
    }

    private sealed record BanDetails(int Minutes, string? Reason);

    private sealed record ShareDetails(decimal Share);
}
