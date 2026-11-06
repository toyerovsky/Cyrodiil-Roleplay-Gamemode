using Roleplay.HungerAndThirst.Common;
using Xunit;

namespace Roleplay.HungerAndThirst.Tests;

public class MealRefusalTests
{
    // Every refusal a mod may get back has the mod's own words, so the satchel never shows an empty line.
    [Fact]
    public void Every_refusal_has_words()
    {
        foreach (MealRefusal refusal in Enum.GetValues<MealRefusal>())
            Assert.False(string.IsNullOrWhiteSpace(MealRefusals.Describe(refusal)), refusal.ToString());
        Assert.Throws<ArgumentOutOfRangeException>(() => MealRefusals.Describe((MealRefusal)255));
    }

    [Theory]
    [InlineData(1000, SustenanceBand.Fine)]
    [InlineData(500, SustenanceBand.Fine)]
    [InlineData(499, SustenanceBand.Low)]
    [InlineData(249, SustenanceBand.Critical)]
    [InlineData(0.5, SustenanceBand.Critical)]
    [InlineData(0, SustenanceBand.Empty)]
    public void A_meter_falls_into_the_band_of_the_warnings(double value, SustenanceBand band)
    {
        Assert.Equal(band, SustenanceScale.BandOf(value));
    }
}
