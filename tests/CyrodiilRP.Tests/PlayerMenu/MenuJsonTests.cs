using System.Text.Json;
using CyrodiilRP.Server.Dice;
using CyrodiilRP.Server.Lore;
using CyrodiilRP.Server.PlayerMenu;

namespace CyrodiilRP.Tests.PlayerMenu;

public sealed class MenuJsonTests
{
    [Fact]
    public void The_lore_tab_goes_out_under_the_names_the_page_reads()
    {
        var preview = new LoreViewJson("Imperial", "public", []);
        var lore = new MenuLoreView(true, "Imperial", preview, [new MenuKnownView(3, "Lucia Gallus")]);

        var root = JsonDocument.Parse(JsonSerializer.Serialize(lore, MenuJson.Default.MenuLoreView)).RootElement;

        Assert.True(root.GetProperty("namePublic").GetBoolean());
        Assert.Equal("Imperial", root.GetProperty("header").GetString());
        Assert.Equal("public", root.GetProperty("preview").GetProperty("level").GetString());
        Assert.Equal(3, root.GetProperty("known")[0].GetProperty("number").GetInt32());
        Assert.Equal("Lucia Gallus", root.GetProperty("known")[0].GetProperty("name").GetString());
    }

    [Fact]
    public void The_dice_tab_goes_out_under_the_names_the_page_reads()
    {
        var view = new DiceView("nord", ["nord"], [], RollNotation.MaxDice);

        var root = JsonDocument.Parse(JsonSerializer.Serialize(view, MenuJson.Default.DiceView)).RootElement;

        Assert.Equal("nord", root.GetProperty("set").GetString());
        Assert.Equal(RollNotation.MaxDice, root.GetProperty("maxDice").GetInt32());
    }
}
