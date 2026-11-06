using System.Text.Json;
using CyrodiilRP.Server.Lore;

namespace CyrodiilRP.Tests.Lore;

public sealed class LoreJsonTests
{
    [Fact]
    public void The_editor_goes_out_under_the_names_the_page_reads()
    {
        var value = new LoreValueJson("race.nord", null, "Nord", "From Skyrim.", null, false, null, "public", false);
        var card = new Dictionary<string, LoreValueJson> { ["seenAs"] = value };
        var answer = new LoreLoadAnswer(true, "canon", false, [], card, null, 16_000, 3, "Marcus Varro", "Imperial", true);

        var root = JsonDocument.Parse(JsonSerializer.Serialize(answer, LoreJson.Page.LoreLoadAnswer)).RootElement;

        Assert.True(root.GetProperty("ok").GetBoolean());
        Assert.False(root.GetProperty("readOnly").GetBoolean());
        Assert.Equal(16_000, root.GetProperty("maxCard").GetInt32());
        Assert.Equal("Imperial", root.GetProperty("header").GetString());
        Assert.True(root.GetProperty("namePublic").GetBoolean());
        Assert.Equal("race.nord", root.GetProperty("card").GetProperty("seenAs").GetProperty("catalogId").GetString());
    }

    [Fact]
    public void A_card_goes_out_under_the_names_the_page_reads()
    {
        var field = new LoreViewFieldJson("notes", "story", "Notes", "Owes Glarthir.", true);
        var view = new LoreViewJson("Imperial", "public", [field]);
        var answer = new LoreCardAnswer("view", true, "4", "Imperial", "stranger", view);

        var root = JsonDocument.Parse(JsonSerializer.Serialize(answer, LoreJson.Page.LoreCardAnswer)).RootElement;

        Assert.Equal("view", root.GetProperty("op").GetString());
        Assert.Equal("Imperial", root.GetProperty("who").GetString());
        Assert.Equal("stranger", root.GetProperty("relation").GetString());
        Assert.Equal("public", root.GetProperty("view").GetProperty("level").GetString());
        Assert.True(root.GetProperty("view").GetProperty("fields")[0].GetProperty("long").GetBoolean());
    }
}
