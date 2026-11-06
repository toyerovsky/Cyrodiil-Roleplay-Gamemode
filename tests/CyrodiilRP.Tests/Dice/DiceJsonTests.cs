using System.Text.Json;
using CyrodiilRP.Server.Dice;

namespace CyrodiilRP.Tests.Dice;

public sealed class DiceJsonTests
{
    [Fact]
    public void The_board_goes_out_under_the_names_the_page_reads()
    {
        var roll = new DiceRollView(1_000, "1d20", [new DieView(20, 17)], 17, "Strength");
        var view = new DiceView("imperial", ["imperial", "nord"], [roll], RollNotation.MaxDice);

        var root = JsonDocument.Parse(JsonSerializer.Serialize(view, DiceJson.Default.DiceView)).RootElement;

        Assert.Equal("imperial", root.GetProperty("set").GetString());
        Assert.Equal(2, root.GetProperty("sets").GetArrayLength());
        Assert.Equal(RollNotation.MaxDice, root.GetProperty("maxDice").GetInt32());

        var shown = root.GetProperty("recent")[0];
        Assert.Equal(1_000, shown.GetProperty("at").GetInt64());
        Assert.Equal("1d20", shown.GetProperty("asked").GetString());
        Assert.Equal(17, shown.GetProperty("total").GetInt32());
        Assert.Equal("Strength", shown.GetProperty("reason").GetString());
        Assert.Equal(20, shown.GetProperty("dice")[0].GetProperty("sides").GetInt32());
        Assert.Equal(17, shown.GetProperty("dice")[0].GetProperty("value").GetInt32());
    }
}
