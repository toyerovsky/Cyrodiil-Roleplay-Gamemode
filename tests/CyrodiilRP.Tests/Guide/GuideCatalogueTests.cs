using System.Text.Json;
using CyrodiilRP.Data;
using CyrodiilRP.Server;
using CyrodiilRP.Server.Guide;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Tests.Fakes;

namespace CyrodiilRP.Tests.Guide;

public sealed class GuideCatalogueTests
{
    private static readonly string Shipped = Path.Combine(AppContext.BaseDirectory, "locales");

    private static readonly Localizer Texts = Localizer.LoadFolder(Shipped, new ListLogger());

    private static readonly string[] Locales = ["en", "pl"];

    [Fact]
    public void Every_topic_reads_whole_in_every_language_with_its_numbers_filled_in()
    {
        var catalogue = new GuideCatalogue(new RpConfig());
        foreach (var topic in Enum.GetValues<GuideTopic>())
        {
            var card = catalogue.Build(topic);
            Assert.Equal(topic, card.Topic);
            Assert.NotEmpty(card.Steps);
            var phrases = new[] { card.Title, card.Purpose }
                .Concat(card.Steps.Select(step => step.Text))
                .Concat(card.Facts.SelectMany(fact => new[] { fact.Label, fact.Value }));
            foreach (var locale in Locales)
            {
                foreach (var phrase in phrases)
                {
                    var text = Texts.Get(locale, phrase);
                    Assert.False(string.IsNullOrWhiteSpace(text), $"{topic} in {locale}: {phrase.Key}");
                    Assert.DoesNotContain("{", text);
                }
            }
        }
    }

    [Fact]
    public void The_numbers_come_from_the_config_the_server_runs()
    {
        var config = new RpConfig();
        config.Stable.Black = 7777;
        config.Specializations.FirstPrice = 0;
        config.Specializations.ChangePrice = 2500;
        config.NoticeBoards.MaxLength = 120;

        var catalogue = new GuideCatalogue(config);
        string Facts(GuideTopic topic) => string.Join(" | ", catalogue.Build(topic).Facts.Select(fact => Texts.Get("en", fact.Value)));

        Assert.Contains("7777 gold", Facts(GuideTopic.Stable));
        Assert.StartsWith("Free | 2500 gold", Facts(GuideTopic.Mentor));
        Assert.Contains("120 letters", Texts.Get("en", catalogue.Build(GuideTopic.NoticeBoard).Steps[1].Text));
    }

    [Theory]
    [InlineData(500, "500")]
    [InlineData(9_999, "9999")]
    [InlineData(100_000, "100\u00A0000")]
    [InlineData(1_000_000, "1\u00A0000\u00A0000")]
    public void A_number_of_five_figures_or_more_is_grouped_by_threes(int value, string expected) =>
        Assert.Equal(expected, GuideCatalogue.Number(value));

    [Theory]
    [InlineData(259_200, "3 days")]
    [InlineData(86_400, "1 day")]
    [InlineData(7_200, "2 h")]
    [InlineData(5_400, "90 min")]
    [InlineData(60, "1 min")]
    [InlineData(30, "30 s")]
    public void A_wait_shows_in_the_largest_unit_it_is_whole_in(int seconds, string expected) =>
        Assert.Equal(expected, Texts.Get("en", GuideCatalogue.Duration(TimeSpan.FromSeconds(seconds))));

    [Fact]
    public void A_card_goes_out_under_the_names_the_page_reads()
    {
        var view = new GuideView(GuideTopic.NoticeBoard, GuideMode.Use, "The notice board", "Anyone can read.",
            [new GuideStepView("Press H.", ["H"])], [new GuideFactView("notices on one board", "10")]);

        var root = JsonDocument.Parse(JsonSerializer.Serialize(view, GuideJson.Default.GuideView)).RootElement;

        Assert.Equal("NoticeBoard", root.GetProperty("topic").GetString());
        Assert.Equal("Use", root.GetProperty("mode").GetString());
        Assert.Equal("The notice board", root.GetProperty("title").GetString());
        Assert.Equal("Anyone can read.", root.GetProperty("purpose").GetString());
        Assert.Equal("H", root.GetProperty("steps")[0].GetProperty("keys")[0].GetString());
        Assert.Equal("Press H.", root.GetProperty("steps")[0].GetProperty("text").GetString());
        Assert.Equal("10", root.GetProperty("facts")[0].GetProperty("value").GetString());
        Assert.Equal("notices on one board", root.GetProperty("facts")[0].GetProperty("label").GetString());
    }
}
