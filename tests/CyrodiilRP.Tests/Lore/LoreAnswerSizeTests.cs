using System.Text;
using System.Text.Json;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Lore;
using CyrodiilRP.Tests.Fakes;

namespace CyrodiilRP.Tests.Lore;

// A card goes to its page in one message, so a card written full in any script still fits one.
public sealed class LoreAnswerSizeTests
{
    private static readonly Localizer Texts = Localizer.LoadFolder(Path.Combine(AppContext.BaseDirectory, "locales"), new ListLogger());
    private static readonly LoreEditor Editor = new(LoreCatalog.Shipped, LoreCatalogMode.Free, allowImplausible: false);
    private static readonly LoreCardText Text = new(LoreCatalog.Shipped, markCustom: false);

    [Theory]
    [InlineData('x')]
    [InlineData('ż')]
    [InlineData('中')]
    public void A_full_card_loads_and_saves_in_one_message(char letter)
    {
        var card = Fill(letter);
        foreach (var locale in Texts.Locales)
        {
            string Translate(Phrase phrase) => Texts.Get(locale, phrase);
            var values = LoreViews.Values(card, Text, LoreCatalog.Shipped, Translate);
            var load = new LoreLoadAnswer(true, "free", false, LoreViews.Fields(Translate), values, null, LoreFields.MaxCardCharacters, card.TextLength,
                "Ri'Zahra Moonpaw", "Khajiit", false);
            var edit = new LoreEditAnswer(true, Translate(LoreText.TooFast), values, card.TextLength, null, false);

            Assert.InRange(Bytes(JsonSerializer.Serialize(load, LoreJson.Page.LoreLoadAnswer)), 1, LoreJson.MaxAnswerBytes);
            Assert.InRange(Bytes(JsonSerializer.Serialize(edit, LoreJson.Page.LoreEditAnswer)), 1, LoreJson.MaxAnswerBytes);
        }
    }

    [Fact]
    public void Letters_go_as_they_are_not_as_escapes()
    {
        var answer = new LoreMessageAnswer(true, "Zażółć gęślą jaźń");

        Assert.Contains("Zażółć gęślą jaźń", JsonSerializer.Serialize(answer, LoreJson.Page.LoreMessageAnswer));
    }

    // Every field written as long as the card lets it, in the given letter.
    private static LoreCard Fill(char letter)
    {
        var card = LoreCard.Blank;
        foreach (var field in LoreFields.All.Where(f => f.Type is LoreFieldType.Text or LoreFieldType.LongText or LoreFieldType.Catalog))
        {
            var room = Math.Min(field.MaxLength, LoreFields.MaxCardCharacters - card.TextLength);
            if (room > 0)
                card = Editor.Set(card, field, new string(letter, room)).Card;
        }

        return card;
    }

    private static int Bytes(string json) => Encoding.UTF8.GetByteCount(json);
}
