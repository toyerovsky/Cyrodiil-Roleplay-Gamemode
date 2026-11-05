using CyrodiilRP.Data;
using CyrodiilRP.Server.Lore;

namespace CyrodiilRP.Tests.Lore;

public sealed class LoreEditorTests
{
    private static readonly LoreCatalog Catalog = LoreCatalog.Shipped;
    private static readonly LoreEditor Canon = new(Catalog, LoreCatalogMode.Canon, allowImplausible: false);
    private static readonly LoreEditor Free = new(Catalog, LoreCatalogMode.Free, allowImplausible: false);

    private static readonly LoreFieldDefinition Race = LoreFields.Get(LoreFields.Race);
    private static readonly LoreFieldDefinition Subtype = LoreFields.Get(LoreFields.Subtype);
    private static readonly LoreFieldDefinition Culture = LoreFields.Get(LoreFields.Culture);
    private static readonly LoreFieldDefinition Homeland = LoreFields.Get(LoreFields.Homeland);
    private static readonly LoreFieldDefinition HomeRegion = LoreFields.Get(LoreFields.HomeRegion);
    private static readonly LoreFieldDefinition Affiliation = LoreFields.Get(LoreFields.Affiliation);
    private static readonly LoreFieldDefinition Rank = LoreFields.Get(LoreFields.Rank);
    private static readonly LoreFieldDefinition Lazy = LoreFields.Get("trait.lazy");
    private static readonly LoreFieldDefinition History = LoreFields.Get("history");
    private static readonly LoreFieldDefinition Notes = LoreFields.Get("notes");
    private static readonly LoreFieldDefinition SeenAs = LoreFields.Get(LoreFields.SeenAs);
    private static readonly LoreFieldDefinition Relationship = LoreFields.Get("relationship");
    private static readonly LoreFieldDefinition Height = LoreFields.Get("height");

    private static readonly int[] FirstRoll = [3, 17, 9, 11, 6];
    private static readonly int[] SecondRoll = [20, 20, 1, 1, 2];

    [Fact]
    public void A_canon_server_takes_the_fields_of_the_lore_list_as_picks_only()
    {
        var refused = Canon.Set(LoreCard.Blank, Race, "Half-dragon");

        Assert.False(refused.Ok);
        Assert.Same(LoreCard.Blank, refused.Card);
        Assert.Empty(refused.Changed);
    }

    [Fact]
    public void A_free_server_takes_words_beside_a_pick_and_canon_drops_them_again()
    {
        var picked = Pick(LoreCard.Blank, Race, "race.khajiit");
        var noted = Free.Set(picked, Race, "from the south");
        Assert.True(noted.Ok);
        Assert.Equal("race.khajiit", noted.Card.Get(Race)!.CatalogId);
        Assert.Equal("from the south", noted.Card.Get(Race)!.Text);

        var dropped = Canon.DropCustom(noted.Card);
        Assert.NotNull(dropped);
        Assert.Equal([Race.Id], dropped.Changed);
        Assert.Null(dropped.Card.Get(Race)!.Text);
        Assert.Equal("race.khajiit", dropped.Card.Get(Race)!.CatalogId);
        Assert.Null(Canon.DropCustom(dropped.Card));
    }

    [Fact]
    public void A_choice_takes_only_its_own_words()
    {
        Assert.False(Canon.Set(LoreCard.Blank, Relationship, "married twice").Ok);

        var wed = Canon.Set(LoreCard.Blank, Relationship, "WED");
        Assert.True(wed.Ok);
        Assert.Equal("wed", wed.Card.Get(Relationship)!.Text);
    }

    [Fact]
    public void A_text_is_cleaned_and_kept_within_its_length()
    {
        var set = Canon.Set(LoreCard.Blank, Height, "  tall \t for a\nBosmer ");
        Assert.Equal("tall for a Bosmer", set.Card.Get(Height)!.Text);

        Assert.False(Canon.Set(LoreCard.Blank, Height, new string('x', Height.MaxLength + 1)).Ok);
        Assert.False(Canon.Set(LoreCard.Blank, Height, "   ").Ok);
    }

    [Fact]
    public void A_long_text_keeps_its_line_breaks()
    {
        var set = Canon.Set(LoreCard.Blank, History, "Born in Bravil.\nRaised in Leyawiin.");

        Assert.Equal("Born in Bravil.\nRaised in Leyawiin.", set.Card.Get(History)!.Text);
    }

    [Fact]
    public void A_whole_card_holds_at_most_its_limit()
    {
        const int Room = 10;
        var full = LoreCard.Blank.With(History, LoreValue.Empty(LoreVisibility.Private) with { Text = new string('x', LoreFields.MaxCardCharacters - Room) });

        Assert.False(Canon.Set(full, Height, new string('x', Room + 1)).Ok);
        Assert.True(Canon.Set(full, Height, new string('x', Room)).Ok);
    }

    [Fact]
    public void A_typed_trait_is_fixed_for_good_and_cannot_be_rolled()
    {
        Assert.False(Canon.Set(LoreCard.Blank, Lazy, (LoreFields.TraitMin - 1).ToString()).Ok);
        Assert.False(Canon.Set(LoreCard.Blank, Lazy, (LoreFields.TraitMax + 1).ToString()).Ok);

        var typed = Canon.Set(LoreCard.Blank, Lazy, "40");
        Assert.True(typed.Ok);
        Assert.Equal(40, typed.Card.Get(Lazy)!.Number);
        Assert.True(typed.Card.Get(Lazy)!.Fixed);

        Assert.NotNull(Canon.CheckRoll(typed.Card, Lazy, out _));
        Assert.False(Canon.Set(typed.Card, Lazy, "50").Ok);
        Assert.False(Canon.Clear(typed.Card, Lazy).Ok);
    }

    [Fact]
    public void A_rolled_trait_takes_one_reroll_and_is_then_fixed()
    {
        Assert.Null(Canon.CheckRoll(LoreCard.Blank, Lazy, out var first));
        Assert.Equal(LoreRollKind.First, first);
        var rolled = Canon.ApplyRoll(LoreCard.Blank, Lazy, FirstRoll);
        Assert.True(rolled.Ok);
        Assert.False(rolled.Card.Get(Lazy)!.Fixed);

        Assert.Null(Canon.CheckRoll(rolled.Card, Lazy, out var second));
        Assert.Equal(LoreRollKind.Reroll, second);
        var rerolled = Canon.ApplyRoll(rolled.Card, Lazy, SecondRoll);
        var value = rerolled.Card.Get(Lazy)!;
        Assert.True(value.Fixed);
        Assert.Equal(SecondRoll.Sum(), value.Number);
        Assert.Equal([FirstRoll.Sum(), SecondRoll.Sum()], value.Rolls.Select(roll => roll.Total));

        Assert.NotNull(Canon.CheckRoll(rerolled.Card, Lazy, out _));
        Assert.False(Canon.ApplyRoll(rerolled.Card, Lazy, FirstRoll).Ok);
    }

    [Fact]
    public void A_first_roll_kept_gives_up_the_reroll()
    {
        var rolled = Canon.ApplyRoll(LoreCard.Blank, Lazy, FirstRoll).Card;

        var kept = Canon.Keep(rolled, Lazy);
        Assert.True(kept.Ok);
        Assert.True(kept.Card.Get(Lazy)!.Fixed);
        Assert.NotNull(Canon.CheckRoll(kept.Card, Lazy, out _));
        Assert.False(Canon.Keep(kept.Card, Lazy).Ok);
        Assert.False(Canon.Keep(LoreCard.Blank, Lazy).Ok);
    }

    [Fact]
    public void A_roll_that_five_d20_cannot_give_is_a_bug()
    {
        Assert.Throws<ArgumentException>(() => Canon.ApplyRoll(LoreCard.Blank, Lazy, [20, 20, 20, 20]));
    }

    [Fact]
    public void Only_the_staff_clear_a_trait_and_the_visibility_stays()
    {
        var typed = Canon.Set(LoreCard.Blank, Lazy, "40").Card;
        Assert.False(Canon.Clear(typed, Lazy).Ok);

        var cleared = Canon.ClearForStaff(typed, Lazy);
        Assert.True(cleared.Ok);
        Assert.True(cleared.Card.Get(Lazy)!.IsEmpty);
        Assert.Null(Canon.CheckRoll(cleared.Card, Lazy, out _));
        Assert.Equal(Lazy.DefaultVisibility, cleared.Card.Get(Lazy)!.Visibility);
    }

    [Fact]
    public void A_cleared_field_keeps_the_owners_visibility()
    {
        var card = Canon.Set(LoreCard.Blank, History, "Born in Bravil.").Card;
        card = Canon.SetVisibility(card, History, LoreVisibility.Public).Card;

        var cleared = Canon.Clear(card, History);

        Assert.True(cleared.Card.Get(History)!.IsEmpty);
        Assert.Equal(LoreVisibility.Public, cleared.Card.Get(History)!.Visibility);
        Assert.False(Canon.Clear(cleared.Card, History).Ok);
    }

    [Fact]
    public void The_notes_and_the_always_public_fields_keep_their_visibility()
    {
        Assert.False(Canon.SetVisibility(LoreCard.Blank, Notes, LoreVisibility.Public).Ok);
        Assert.False(Canon.SetVisibility(LoreCard.Blank, SeenAs, LoreVisibility.Private).Ok);
        Assert.True(Canon.SetVisibility(LoreCard.Blank, History, LoreVisibility.Public).Ok);
    }

    [Fact]
    public void A_kind_needs_the_race_and_offers_only_the_races_own_that_fit_the_game()
    {
        Canon.Candidates(LoreCard.Blank, Subtype, string.Empty, LoreEditor.MaxOptions, out var problem);
        Assert.NotNull(problem);

        var khajiit = Pick(LoreCard.Blank, Race, "race.khajiit");
        var kinds = Canon.Candidates(khajiit, Subtype, string.Empty, LoreEditor.MaxOptions, out _).Select(entry => entry.Id).ToList();
        Assert.Contains("khajiit.furstock.cathay", kinds);
        Assert.DoesNotContain("khajiit.furstock.senche", kinds);
        Assert.All(kinds, id => Assert.True(Catalog.TryGet(id, out var entry) && entry.Race is null or "Khajiit"));

        var all = new LoreEditor(Catalog, LoreCatalogMode.Canon, allowImplausible: true);
        Assert.Contains("khajiit.furstock.senche", all.Candidates(khajiit, Subtype, string.Empty, LoreEditor.MaxOptions, out _).Select(entry => entry.Id));
    }

    [Fact]
    public void The_future_is_never_offered_and_the_past_only_as_heritage()
    {
        var factions = Canon.Candidates(LoreCard.Blank, Affiliation, string.Empty, LoreEditor.MaxOptions, out _);
        Assert.All(factions, entry => Assert.Equal(LoreEra.Current, entry.Era));
        Assert.DoesNotContain(factions, entry => entry.Id == "faction.stormcloaks");

        var dunmer = Pick(LoreCard.Blank, Race, "race.dunmer");
        var cultures = Canon.Candidates(dunmer, Culture, string.Empty, LoreEditor.MaxOptions, out _);
        Assert.Contains(cultures, entry => entry.Id == "dunmer.tribe.mabrigash");
        Assert.DoesNotContain(cultures, entry => entry.Era == LoreEra.Future);
    }

    [Fact]
    public void A_mixed_ancestry_is_never_a_pick()
    {
        var nord = Pick(LoreCard.Blank, Race, "race.nord");

        Assert.DoesNotContain(Canon.Candidates(nord, Subtype, string.Empty, LoreEditor.MaxOptions, out _),
            entry => entry.Race == LoreEntry.MixedRace);
    }

    [Fact]
    public void A_home_region_lies_inside_the_homeland()
    {
        Canon.Candidates(LoreCard.Blank, HomeRegion, string.Empty, LoreEditor.MaxOptions, out var problem);
        Assert.NotNull(problem);

        var elsweyr = Pick(LoreCard.Blank, Homeland, "province.elsweyr");
        var regions = Canon.Candidates(elsweyr, HomeRegion, string.Empty, LoreEditor.MaxOptions, out _);
        Assert.Contains(regions, entry => entry.Id == "khajiit.region.anequina");
        Assert.All(regions, entry => Assert.True(Canon.IsInside(entry, "province.elsweyr")));

        Assert.False(Canon.Pick(elsweyr, HomeRegion, Get("region.heartlands")).Ok);
    }

    [Fact]
    public void A_rank_belongs_to_the_affiliation_and_a_title_of_its_own_to_anyone()
    {
        var fighter = Pick(LoreCard.Blank, Affiliation, "faction.fighters-guild");
        var ranks = Canon.Candidates(fighter, Rank, string.Empty, LoreEditor.MaxOptions, out _);

        Assert.Contains(ranks, entry => entry.Id == "rank.fighters-guild.associate");
        Assert.Contains(ranks, entry => entry.Id == "title.emperor");
        Assert.All(ranks, entry => Assert.True(entry.Parent == "faction.fighters-guild" || entry is { Kind: LoreKind.Title, Parent: null }));
    }

    [Fact]
    public void A_new_race_empties_the_picks_of_the_old_one()
    {
        var khajiit = Pick(LoreCard.Blank, Race, "race.khajiit");
        var cathay = Pick(khajiit, Subtype, "khajiit.furstock.cathay");

        var nord = Canon.Pick(cathay, Race, Get("race.nord"));

        Assert.True(nord.Ok);
        Assert.Equal([Race.Id, Subtype.Id], nord.Changed);
        Assert.True(nord.Card.Get(Subtype)!.IsEmpty);
        Assert.Equal("race.nord", nord.Card.Get(Race)!.CatalogId);
    }

    [Fact]
    public void An_entry_of_another_kind_does_not_fit_a_field()
    {
        Assert.False(Canon.Pick(LoreCard.Blank, Race, Get("province.elsweyr")).Ok);
    }

    [Fact]
    public void The_native_province_of_each_race_is_in_the_list()
    {
        foreach (var race in Catalog.Entries.Where(entry => entry.Kind == LoreKind.Race))
        {
            var province = LoreEditor.FindNativeProvince(race.Race ?? race.Name);
            Assert.True(province is not null && Catalog.TryGet(province, out _), $"{race.Id} has no native province.");
        }
    }

    private static LoreEntry Get(string id) => Catalog.TryGet(id, out var entry) ? entry : throw new ArgumentException($"No {id} in the lore list.");

    private static LoreCard Pick(LoreCard card, LoreFieldDefinition field, string id)
    {
        var edit = Canon.Pick(card, field, Get(id));
        Assert.True(edit.Ok, $"{id} did not fit {field.Id}.");
        return edit.Card;
    }
}
