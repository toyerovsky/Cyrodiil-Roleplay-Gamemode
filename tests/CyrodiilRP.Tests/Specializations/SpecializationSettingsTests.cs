using CyrodiilRP.Server;
using OblivionMP.SDK.Common.Skills;

namespace CyrodiilRP.Tests.Specializations;

public sealed class SpecializationSettingsTests
{
    [Fact]
    public void The_defaults_hold()
    {
        var settings = new SpecializationSettings();

        settings.RequireValid();
#if DEBUG
        Assert.Equal(TimeSpan.FromSeconds(30), settings.ChangeCooldown);
#else
        Assert.Equal(TimeSpan.FromSeconds(60), settings.ChangeCooldown);
#endif
        Assert.Equal(TimeSpan.FromSeconds(259200), new SpecializationSettings { ChangeCooldownSeconds = 259200 }.ChangeCooldown);
    }

    [Fact]
    public void The_shipped_config_holds_the_defaults()
    {
        var shipped = File.ReadAllText(Path.Combine(Fakes.Repository.Root, "src", "CyrodiilRP.Server", "config.json"));
        var defaults = new SpecializationSettings();

        foreach (var (key, value) in new Dictionary<string, int>
                 {
                     ["firstPrice"] = defaults.FirstPrice, ["changePrice"] = defaults.ChangePrice,
                     ["trainedSkillCap"] = defaults.TrainedSkillCap,
                     ["limitedSkillCap"] = defaults.LimitedSkillCap, ["trainedAttributeCap"] = defaults.TrainedAttributeCap,
                     ["limitedAttributeCap"] = defaults.LimitedAttributeCap, ["unchosenSkillCap"] = defaults.UnchosenSkillCap,
                 })
            Assert.Contains($"\"{key}\": {value},", shipped);

        Assert.Contains($"\"potions\": \"{defaults.Potions}\"", shipped);
        Assert.DoesNotContain("\"changeCooldownSeconds\":", shipped);
    }

    private static readonly Dictionary<string, SpecializationSettings> Broken = new()
    {
        ["a first price below nothing"] = new() { FirstPrice = -1 },
        ["a change price below nothing"] = new() { ChangePrice = -1 },
        ["a cooldown below nothing"] = new() { ChangeCooldownSeconds = -1 },
        ["a trained cap over 100"] = new() { TrainedSkillCap = 101 },
        ["a limited cap below nothing"] = new() { LimitedSkillCap = -1 },
        ["an attribute cap over 100"] = new() { TrainedAttributeCap = 101 },
        ["a limited attribute cap below nothing"] = new() { LimitedAttributeCap = -1 },
        ["an unchosen cap over 100"] = new() { UnchosenSkillCap = 101 },
        ["no such potion rule"] = new() { Potions = (PotionRule)7 },
    };

    [Theory]
    [InlineData("a first price below nothing")]
    [InlineData("a change price below nothing")]
    [InlineData("a cooldown below nothing")]
    [InlineData("a trained cap over 100")]
    [InlineData("a limited cap below nothing")]
    [InlineData("an attribute cap over 100")]
    [InlineData("a limited attribute cap below nothing")]
    [InlineData("an unchosen cap over 100")]
    [InlineData("no such potion rule")]
    public void A_wrong_value_stops_the_mod(string name)
    {
        Assert.Throws<InvalidOperationException>(() => Broken[name].RequireValid());
    }

    [Fact]
    public void Free_changes_at_once_and_caps_of_0_and_100_are_allowed()
    {
        new SpecializationSettings { ChangePrice = 0, ChangeCooldownSeconds = 0, LimitedSkillCap = 0, TrainedSkillCap = 100 }.RequireValid();
    }
}
