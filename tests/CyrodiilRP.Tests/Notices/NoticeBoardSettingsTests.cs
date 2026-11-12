using CyrodiilRP.Data.Notices;
using CyrodiilRP.Server;

namespace CyrodiilRP.Tests.Notices;

public sealed class NoticeBoardSettingsTests
{
    [Fact]
    public void The_defaults_hold()
    {
        new NoticeBoardSettings().Check();
    }

    [Fact]
    public void The_shipped_config_holds_the_defaults()
    {
        var shipped = File.ReadAllText(Path.Combine(Fakes.Repository.Root, "src", "CyrodiilRP.Server", "config.json"));
        var defaults = new NoticeBoardSettings();

        foreach (var (key, value) in new Dictionary<string, object>
                 {
                     ["shown"] = defaults.Shown, ["minLength"] = defaults.MinLength, ["maxLength"] = defaults.MaxLength,
                     ["perCharacter"] = defaults.PerCharacter, ["cooldownMinutes"] = defaults.CooldownMinutes,
                     ["expiryDays"] = defaults.ExpiryDays,
                 })
            Assert.Contains($"\"{key}\": {value},", shipped);

        Assert.Contains($"\"asset\": \"{defaults.Asset}\"", shipped);
    }

    private static readonly Dictionary<string, NoticeBoardSettings> Broken = new()
    {
        ["nothing shown"] = new() { Shown = 0 },
        ["no notice a character"] = new() { PerCharacter = 0 },
        ["a wait below nothing"] = new() { CooldownMinutes = -1 },
        ["no day up"] = new() { ExpiryDays = 0 },
        ["no least length"] = new() { MinLength = 0 },
        ["a least over the most"] = new() { MinLength = 20, MaxLength = 10 },
        ["longer than the column"] = new() { MaxLength = NoticeBoardBook.MostTextLength + 1 },
        ["a board past one message"] = new() { Shown = 100, MaxLength = 1000 },
        ["no mesh"] = new() { Asset = " " },
    };

    [Theory]
    [InlineData("nothing shown")]
    [InlineData("no notice a character")]
    [InlineData("a wait below nothing")]
    [InlineData("no day up")]
    [InlineData("no least length")]
    [InlineData("a least over the most")]
    [InlineData("longer than the column")]
    [InlineData("a board past one message")]
    [InlineData("no mesh")]
    public void A_setting_that_makes_no_rule_stops_the_mod_at_start(string setting)
    {
        Assert.Throws<InvalidOperationException>(Broken[setting].Check);
    }

    [Fact]
    public void A_full_board_at_the_most_it_may_hold_fits_one_message()
    {
        var settings = new NoticeBoardSettings { Shown = 15, MaxLength = 1000 };

        settings.Check();
        Assert.True((long)settings.Shown * settings.MaxLength * 3 < ushort.MaxValue);
    }
}
