using System.Text.Json;
using CyrodiilRP.Server.Exchange;

namespace CyrodiilRP.Tests.Exchange;

public sealed class ExchangeJsonTests
{
    [Fact]
    public void A_view_goes_out_under_the_names_the_page_reads()
    {
        var item = new ExchangeItemView("key", "Iron Sword", true, 2);
        var offer = new ExchangeOfferView(40, [item], true);
        var view = new ExchangeView(3, "Lucia Gallus", 120, 100, 500, [item], offer, offer);

        var root = JsonDocument.Parse(JsonSerializer.Serialize(view, ExchangeJson.Default.ExchangeView)).RootElement;

        Assert.Equal(3, root.GetProperty("version").GetInt32());
        Assert.Equal("Lucia Gallus", root.GetProperty("other").GetString());
        Assert.Equal(120, root.GetProperty("carried").GetInt32());
        Assert.Equal(100, root.GetProperty("max").GetInt32());
        Assert.Equal(500, root.GetProperty("confirmInMs").GetInt64());
        Assert.Equal(40, root.GetProperty("mine").GetProperty("gold").GetInt32());
        Assert.True(root.GetProperty("theirs").GetProperty("confirmed").GetBoolean());

        var shown = root.GetProperty("mine").GetProperty("items")[0];
        Assert.Equal("key", shown.GetProperty("key").GetString());
        Assert.Equal("Iron Sword", shown.GetProperty("name").GetString());
        Assert.True(shown.GetProperty("used").GetBoolean());
        Assert.Equal(2, shown.GetProperty("count").GetInt32());
        Assert.Equal(1, root.GetProperty("items").GetArrayLength());
    }
}
