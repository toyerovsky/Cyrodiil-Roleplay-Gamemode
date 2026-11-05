using System.Text;
using System.Text.Json;
using CyrodiilRP.Server.Lore;

namespace CyrodiilRP.Tests.Lore;

public sealed class LoreCatalogTests
{
    // MESOL's lore list as shipped.
    private const int ShippedEntries = 859;

    // A string goes over the network with its length in two bytes, so an answer stays well under 64 KiB.
    private const int MaxAnswerBytes = 60_000;

    private const string BadEra = """{"entries":[{"id":"x.y","race":null,"kind":"race","name":"Y","parent":null,"summary":"","era":"someday"}]}""";
    private const string BadKind = """{"entries":[{"id":"x.y","race":null,"kind":"dragon","name":"Y","parent":null,"summary":"","era":"current"}]}""";

    [Fact]
    public void The_shipped_list_loads_whole()
    {
        Assert.Equal(ShippedEntries, LoreCatalog.Shipped.Count);
        Assert.True(LoreCatalog.Shipped.TryGet("RACE.KHAJIIT", out var khajiit));
        Assert.Equal("Khajiit", khajiit.Name);
    }

    [Fact]
    public void An_entry_that_does_not_read_is_a_mistake_in_the_file()
    {
        Assert.Throws<FormatException>(() => LoreCatalog.Load(BadEra));
        Assert.Throws<FormatException>(() => LoreCatalog.Load(BadKind));
    }

    [Fact]
    public void Every_parent_named_is_in_the_list()
    {
        var missing = LoreCatalog.Shipped.Entries
            .Where(entry => entry.Parent is { } parent && !LoreCatalog.Shipped.TryGet(parent, out _))
            .Select(entry => entry.Id)
            .ToList();

        Assert.Empty(missing);
    }

    [Fact]
    public void A_search_puts_the_exact_name_first()
    {
        var found = LoreCatalog.Shipped.Search([LoreKind.Race], "Nord", _ => true, LoreEditor.MaxOptions);

        Assert.Equal("race.nord", found[0].Id);
    }

    // The biggest pickers with the races that give them the most, on a free server that offers every kind.
    [Fact]
    public void Every_pickers_answer_fits_one_message()
    {
        var editor = new LoreEditor(LoreCatalog.Shipped, LoreCatalogMode.Free, allowImplausible: true);
        var races = LoreCatalog.Shipped.Entries.Where(entry => entry.Kind == LoreKind.Race).ToList();
        var provinces = LoreCatalog.Shipped.Entries.Where(entry => entry.Kind == LoreKind.Province).ToList();
        var factions = LoreCatalog.Shipped.Entries.Where(entry => entry.Kind == LoreKind.Faction).ToList();
        var largest = 0;
        foreach (var field in LoreFields.All.Where(f => f.Type == LoreFieldType.Catalog))
        {
            foreach (var race in races)
            {
                foreach (var place in provinces.Take(field.Id == LoreFields.HomeRegion ? provinces.Count : 1))
                {
                    foreach (var faction in factions.Take(field.Id == LoreFields.Rank ? factions.Count : 1))
                    {
                        var card = LoreCard.Blank;
                        card = editor.Pick(card, LoreFields.Get(LoreFields.Race), race).Card;
                        card = editor.Pick(card, LoreFields.Get(LoreFields.Homeland), place).Card;
                        card = editor.Pick(card, LoreFields.Get(LoreFields.Affiliation), faction).Card;
                        var options = editor.Candidates(card, field, string.Empty, LoreEditor.MaxOptions, out _)
                            .Select(entry => LoreViews.Option(entry, "Other Tamrielic Provinces"))
                            .ToList();
                        var json = JsonSerializer.Serialize(new LoreOptionsAnswer(true, field.Id, options, null), LoreJson.Default.LoreOptionsAnswer);
                        largest = Math.Max(largest, Encoding.UTF8.GetByteCount(json));
                    }
                }
            }
        }

        Assert.InRange(largest, 1, MaxAnswerBytes);
    }
}
