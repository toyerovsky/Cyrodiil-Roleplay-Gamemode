using System.Text.Json;
using CyrodiilRP.Server.Admin;
using CyrodiilRP.Server.Dice;
using CyrodiilRP.Server.Lore;

namespace CyrodiilRP.Tests.Admin;

public sealed class AdminJsonTests
{
    [Fact]
    public void A_lore_card_goes_out_under_the_names_the_panel_reads()
    {
        var field = new LoreViewFieldJson("notes", "story", "Notes", "Owes Glarthir.", true);
        var view = new AdminLoreView(4, "Brannoc", true, new LoreViewJson("Brannoc", "staff", [field]));

        var root = JsonDocument.Parse(JsonSerializer.Serialize(view, AdminJson.Letters.AdminLoreView)).RootElement;

        Assert.Equal(4, root.GetProperty("number").GetInt32());
        Assert.Equal("Brannoc", root.GetProperty("name").GetString());
        Assert.True(root.GetProperty("namePublic").GetBoolean());
        Assert.Equal("staff", root.GetProperty("view").GetProperty("level").GetString());
        Assert.Equal("notes", root.GetProperty("view").GetProperty("fields")[0].GetProperty("id").GetString());
    }

    [Fact]
    public void The_roll_log_goes_out_under_the_names_the_panel_reads()
    {
        var roll = new AdminRollView(9, 1_000, "Brannoc", "1d20", [new DieView(20, 17)], 17, null, "L_Tamriel");
        var page = new AdminRollsView([roll], true, false);

        var root = JsonDocument.Parse(JsonSerializer.Serialize(page, AdminJson.Default.AdminRollsView)).RootElement;

        Assert.True(root.GetProperty("more").GetBoolean());
        Assert.False(root.GetProperty("appended").GetBoolean());
        var shown = root.GetProperty("rolls")[0];
        Assert.Equal(9, shown.GetProperty("id").GetInt64());
        Assert.Equal("Brannoc", shown.GetProperty("character").GetString());
        Assert.Equal("L_Tamriel", shown.GetProperty("area").GetString());
        Assert.Equal(17, shown.GetProperty("dice")[0].GetProperty("value").GetInt32());
    }

    [Fact]
    public void The_roll_filter_is_read_under_the_names_the_panel_sends()
    {
        var filter = JsonSerializer.Deserialize("""{"player":"Bran","before":7}""", AdminJson.Default.AdminRollFilter);

        Assert.Equal(new AdminRollFilter("Bran", 7), filter);
    }
}
