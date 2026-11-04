using System.Text.Json;
using CyrodiilRP.Server.Exchange;

namespace CyrodiilRP.Tests.Exchange;

public sealed class ExchangeJsonTests
{
    [Fact]
    public void A_view_goes_out_under_the_names_the_page_reads()
    {
        var offer = new ExchangeOfferView(40, true);
        var view = new ExchangeView(3, "Lucia Gallus", 120, 100, 500, offer, offer);

        var root = JsonDocument.Parse(JsonSerializer.Serialize(view, ExchangeJson.Default.ExchangeView)).RootElement;

        Assert.Equal(3, root.GetProperty("version").GetInt32());
        Assert.Equal("Lucia Gallus", root.GetProperty("other").GetString());
        Assert.Equal(120, root.GetProperty("carried").GetInt32());
        Assert.Equal(100, root.GetProperty("max").GetInt32());
        Assert.Equal(500, root.GetProperty("confirmInMs").GetInt64());
        Assert.Equal(40, root.GetProperty("mine").GetProperty("gold").GetInt32());
        Assert.True(root.GetProperty("theirs").GetProperty("confirmed").GetBoolean());
    }
}
