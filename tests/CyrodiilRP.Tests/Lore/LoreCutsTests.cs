using CyrodiilRP.Data;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Lore;
using CyrodiilRP.Tests.Fakes;

namespace CyrodiilRP.Tests.Lore;

public sealed class LoreCutsTests
{
    private const string Name = "Ri'Zahra Moonpaw";
    private const string Label = "Khajiit";

    private static readonly Localizer Texts = Localizer.LoadFolder(Path.Combine(AppContext.BaseDirectory, "locales"), new ListLogger());
    private static readonly LoreCardText Text = new(LoreCatalog.Shipped, markCustom: false);

    private static readonly LoreFieldDefinition Height = LoreFields.Get("height");
    private static readonly LoreFieldDefinition History = LoreFields.Get("history");
    private static readonly LoreFieldDefinition Notes = LoreFields.Get("notes");
    private static readonly LoreFieldDefinition SeenAs = LoreFields.Get(LoreFields.SeenAs);
    private static readonly LoreFieldDefinition Title = LoreFields.Get(LoreFields.Title);
    private static readonly LoreFieldDefinition Status = LoreFields.Get(LoreFields.Status);

    private static readonly LoreCard Card = LoreCard.Blank
        .With(Height, Value("tall", LoreVisibility.Public))
        .With(History, Value("Born in Bravil.", LoreVisibility.Private))
        .With(Notes, Value("Owes Jo 20 gold.", LoreVisibility.Private))
        .With(SeenAs, Value("a scarred Khajiit with a merchant's sash", LoreVisibility.Private))
        .With(Title, Value("Caravan guard", LoreVisibility.Private))
        .With(Status, Value("ic", LoreVisibility.Private));

    [Fact]
    public void A_stranger_reads_the_public_fields_an_acquaintance_the_private_ones_too_and_nobody_the_notes()
    {
        Assert.Equal([SeenAs.Id, Height.Id, Status.Id], Visible(LoreViewer.Stranger));
        Assert.Equal([Title.Id, SeenAs.Id, Height.Id, History.Id, Status.Id], Visible(LoreViewer.Acquaintance));
        Assert.Contains(Notes.Id, Visible(LoreViewer.Owner));
        Assert.Contains(Notes.Id, Visible(LoreViewer.Staff));
    }

    [Fact]
    public void An_empty_field_is_never_shown()
    {
        var cleared = Card.With(Height, LoreValue.Empty(LoreVisibility.Public));

        Assert.DoesNotContain(Height.Id, LoreCuts.Visible(cleared, LoreViewer.Stranger).Select(item => item.Field.Id));
    }

    [Fact]
    public void A_stranger_reads_what_the_character_is_seen_as_and_one_who_knows_the_name_reads_it_with_the_title()
    {
        Assert.Equal("a scarred Khajiit with a merchant's sash (in character)",
            Read(Text.Header(Card, Name, nameKnown: false, LoreViewer.Stranger, Label)));
        Assert.Equal($"{Name}, Caravan guard (in character)", Read(Text.Header(Card, Name, nameKnown: true, LoreViewer.Acquaintance, Label)));
    }

    [Fact]
    public void A_public_name_shows_to_a_stranger_without_the_private_title()
    {
        Assert.Equal($"{Name} (in character)", Read(Text.Header(Card, Name, nameKnown: true, LoreViewer.Stranger, Label)));
    }

    [Fact]
    public void A_card_with_nothing_on_it_shows_the_race_to_a_stranger()
    {
        Assert.Equal(Label, Read(Text.Header(LoreCard.Blank, Name, nameKnown: false, LoreViewer.Stranger, Label)));
    }

    [Fact]
    public void A_view_leaves_out_what_the_header_already_says()
    {
        var view = LoreViews.View(Card, LoreViewer.Acquaintance, LoreText.Plain(Name), Text, Read);

        Assert.Equal("private", view.Level);
        Assert.Equal([Height.Id, History.Id], view.Fields.Select(field => field.Id));
        Assert.True(view.Fields.Single(field => field.Id == History.Id).Long);
    }

    [Fact]
    public void A_trait_reads_its_number_how_strongly_and_how_it_was_set()
    {
        var lazy = LoreFields.Get("trait.lazy");
        var rolled = new LoreValue(null, null, 46, false, [new LoreTraitRoll([3, 17, 9, 11, 6], 46)], LoreVisibility.Private);

        Assert.Equal("46 (notably, rolled)", Read(Text.Display(lazy, rolled)));
        Assert.Equal("46 (wyraźnie, wylosowane)", Texts.Get("pl", Text.Display(lazy, rolled)));
    }

    private static IReadOnlyList<string> Visible(LoreViewer viewer) => LoreCuts.Visible(Card, viewer).Select(item => item.Field.Id).ToList();

    private static LoreValue Value(string text, LoreVisibility visibility) => LoreValue.Empty(visibility) with { Text = text };

    private static string Read(Phrase phrase) => Texts.Get("en", phrase);
}
