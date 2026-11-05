using CyrodiilRP.Data;
using CyrodiilRP.Server.Lore;
using CyrodiilRP.Tests.Fakes;
using CyrodiilRP.Server.Localization;

namespace CyrodiilRP.Tests.Lore;

public sealed class LoreFieldsTests
{
    private static readonly Localizer Texts = Localizer.LoadFolder(Path.Combine(AppContext.BaseDirectory, "locales"), new ListLogger());

    [Fact]
    public void A_field_is_named_once_and_its_id_fits_its_column()
    {
        Assert.Equal(LoreFields.All.Count, LoreFields.All.Select(field => field.Id.ToLowerInvariant()).Distinct().Count());
        Assert.All(LoreFields.All, field => Assert.InRange(field.Id.Length, 1, LoreFields.MaxIdLength));
    }

    // The name is the character's own, never a field of the card.
    [Fact]
    public void The_card_has_no_first_or_last_name()
    {
        Assert.False(LoreFields.TryGet("firstName", out _));
        Assert.False(LoreFields.TryGet("lastName", out _));
    }

    [Fact]
    public void Every_field_the_lore_list_fills_has_kinds_and_every_choice_has_words()
    {
        Assert.All(LoreFields.All.Where(f => f.Type == LoreFieldType.Catalog), field => Assert.NotEmpty(field.Kinds));
        Assert.All(LoreFields.All.Where(f => f.Type == LoreFieldType.Choice), field => Assert.NotEmpty(field.Choices));
        Assert.All(LoreFields.All.Where(f => f.Type == LoreFieldType.Trait), field => Assert.NotNull(field.Group));
        Assert.All(LoreFields.All.Where(f => f.Needs is not null), field => Assert.True(LoreFields.TryGet(field.Needs, out _)));
        Assert.All(LoreFields.Dependents.SelectMany(pair => pair.Value.Prepend(pair.Key)), id => Assert.True(LoreFields.TryGet(id, out _)));
    }

    [Fact]
    public void Only_the_notes_are_the_owners_alone_and_they_never_change_hands()
    {
        var notes = Assert.Single(LoreFields.All, field => field.OwnerOnly);

        Assert.Equal("notes", notes.Id);
        Assert.True(notes.Locked);
    }

    [Fact]
    public void Every_label_hint_and_word_reads_in_both_languages()
    {
        foreach (var locale in Texts.Locales)
        {
            foreach (var field in LoreFields.All)
            {
                Assert.NotEqual(field.Label.Key, Texts.Get(locale, field.Label));
                Assert.NotEqual(field.Hint.Key, Texts.Get(locale, field.Hint));
                Assert.All(field.Choices, choice => Assert.NotEqual(choice.Word.Key, Texts.Get(locale, choice.Word)));
            }
        }
    }

    [Theory]
    [InlineData(LoreFields.TraitMin, "faintly")]
    [InlineData(20, "faintly")]
    [InlineData(21, "somewhat")]
    [InlineData(60, "notably")]
    [InlineData(80, "strongly")]
    [InlineData(LoreFields.TraitMax, "overwhelmingly")]
    public void A_trait_reads_how_strongly_it_drives_the_character(int value, string word)
    {
        Assert.Equal(word, Texts.Get("en", LoreFields.TraitWord(value)));
    }

    [Fact]
    public void The_page_gets_every_field_in_the_readers_language_with_two_words_for_who_sees_it()
    {
        var fields = LoreViews.Fields(phrase => Texts.Get("pl", phrase));

        Assert.Equal(LoreFields.All.Count, fields.Count);
        Assert.All(fields, field => Assert.Contains(field.Vis, new[] { "public", "private" }));
        Assert.Equal("Leniwy", fields.Single(field => field.Id == "trait.lazy").Label);
        Assert.Equal("Przywary i nawyki", fields.Single(field => field.Id == "trait.lazy").Group);
        Assert.Equal("Stan wolny", fields.Single(field => field.Id == "relationship").Choices![0].Name);
        Assert.Equal(LoreVisibility.Private, LoreFields.Get("history").DefaultVisibility);
    }
}
