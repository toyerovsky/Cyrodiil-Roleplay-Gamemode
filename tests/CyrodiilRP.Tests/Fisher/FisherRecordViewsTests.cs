using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using CyrodiilRP.Data;
using CyrodiilRP.Data.Fishing;
using CyrodiilRP.Server.Fisher;
using CyrodiilRP.Server.Fishing;
using CyrodiilRP.Server.Lore;

namespace CyrodiilRP.Tests.Fisher;

public sealed class FisherRecordViewsTests
{
    private const long ReaderId = 7;
    private const string ReaderName = "Marius Caro";
    private const FishSpecies Pike = FishSpecies.Pike;
    private const int Crowd = 1_000_000;

    private static readonly DateTimeOffset Day = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);
    private static readonly RecordReader Stranger = Reader();

    [Fact]
    public void Every_species_stands_in_the_catalogues_order_and_one_nobody_caught_has_no_fish()
    {
        var view = RecordBoardViews.Build(Board(Pike, Holder(1, 90)), Stranger, Show);

        Assert.Equal(FishCatalog.All.Select(fish => fish.Species), view.Species.Select(species => species.Species));
        Assert.Equal(FishCatalog.All.Select(fish => fish.ItemName), view.Species.Select(species => species.Name));
        Assert.All(view.Species.Where(species => species.Species != Pike), species =>
        {
            Assert.Empty(species.Top);
            Assert.Null(species.Own);
        });
    }

    [Fact]
    public void The_longest_of_a_species_are_listed_the_best_first_each_by_its_rank()
    {
        var holders = new[] { Holder(1, 60), Holder(2, 104), Holder(3, 88), Holder(4, 71), Holder(5, 95), Holder(6, 50) };

        var pike = Find(RecordBoardViews.Build(Board(Pike, holders), Stranger, Show));

        Assert.Equal(RecordBoardRules.TopCount, pike.Top.Count);
        Assert.Equal([104, 95, 88, 71, 60], pike.Top.Select(row => row.LengthCm));
        Assert.Equal([1, 2, 3, 4, 5], pike.Top.Select(row => row.Rank));
        Assert.Equal(["Angler 2", "Angler 5", "Angler 3", "Angler 4", "Angler 1"], pike.Top.Select(row => row.Name));
        Assert.All(pike.Top, row => Assert.False(row.Mine));
        Assert.Null(pike.Own);
    }

    [Fact]
    public void Fish_of_one_length_share_a_rank_and_stand_in_the_order_they_were_caught()
    {
        var holders = new[] { Holder(1, 80, Day.AddDays(2)), Holder(2, 80, Day), Holder(3, 95), Holder(4, 80, Day.AddDays(1)), Holder(5, 70) };

        var pike = Find(RecordBoardViews.Build(Board(Pike, holders), Stranger, Show));

        Assert.Equal(["Angler 3", "Angler 2", "Angler 4", "Angler 1", "Angler 5"], pike.Top.Select(row => row.Name));
        Assert.Equal([1, 2, 2, 2, 5], pike.Top.Select(row => row.Rank));
    }

    [Fact]
    public void The_readers_own_best_is_marked_on_the_board_and_named_as_they_read_themselves()
    {
        var reader = Reader(new FishRecord(88, 5600, 3, Day));

        var pike = Find(RecordBoardViews.Build(Board(Pike, Holder(1, 95), Holder(ReaderId, 88), Holder(2, 60)), reader, Show));

        Assert.Equal([false, true, false], pike.Top.Select(row => row.Mine));
        Assert.Equal(ReaderName, pike.Top[1].Name);
        Assert.Equal(2, pike.Top[1].Rank);
        Assert.Null(pike.Own);
    }

    [Fact]
    public void The_readers_best_under_the_board_is_ranked_by_how_many_hold_a_longer_one()
    {
        var holders = Enumerable.Range(1, RecordBoardRules.TopCount).Select(id => Holder(id, 100 - id)).ToArray();
        var lengths = holders.Select(holder => new FishLengthCount(Pike.ToString(), holder.LengthCm, 1))
            .Append(new FishLengthCount(Pike.ToString(), 80, 4))
            .Append(new FishLengthCount(Pike.ToString(), 74, 3))
            .Append(new FishLengthCount(Pike.ToString(), 60, 9));
        var board = new RecordBoard(new Dictionary<FishSpecies, SpeciesBoard> { [Pike] = new(holders, [.. lengths]) });
        var reader = Reader(new FishRecord(74, 3400, 2, Day.AddDays(3)));

        var pike = Find(RecordBoardViews.Build(board, reader, Show));

        Assert.All(pike.Top, row => Assert.False(row.Mine));
        var own = Assert.IsType<FisherRecordView>(pike.Own);
        Assert.Equal(RecordBoardRules.TopCount + 4 + 1, own.Rank);
        Assert.Equal(ReaderName, own.Name);
        Assert.Equal(74, own.LengthCm);
        Assert.Equal(3400, own.Grams);
        Assert.Equal(Day.AddDays(3).ToUnixTimeMilliseconds(), own.At);
        Assert.True(own.Mine);
    }

    [Fact]
    public void A_fish_as_long_as_the_fifth_but_caught_later_stands_under_the_board_with_the_same_rank()
    {
        var holders = Enumerable.Range(1, RecordBoardRules.TopCount).Select(id => Holder(id, 80, Day)).ToArray();
        var reader = Reader(new FishRecord(80, 4200, 1, Day.AddDays(1)));

        var pike = Find(RecordBoardViews.Build(Board(Pike, holders), reader, Show));

        Assert.All(pike.Top, row => Assert.Equal(1, row.Rank));
        Assert.All(pike.Top, row => Assert.False(row.Mine));
        Assert.Equal(1, pike.Own?.Rank);
    }

    [Fact]
    public void A_catch_the_board_has_not_read_yet_already_stands_among_the_readers_own()
    {
        var reader = Reader(new FishRecord(97, 7600, 4, Day.AddDays(5)));

        var pike = Find(RecordBoardViews.Build(Board(Pike, Holder(1, 99), Holder(2, 98), Holder(3, 97), Holder(4, 96), Holder(ReaderId, 70)), reader, Show));

        Assert.Equal([99, 98, 97, 97, 96], pike.Top.Select(row => row.LengthCm));
        Assert.Equal([1, 2, 3, 3, 5], pike.Top.Select(row => row.Rank));
        Assert.Equal([false, false, false, true, false], pike.Top.Select(row => row.Mine));
        Assert.Null(pike.Own);
    }

    [Fact]
    public void A_reader_whose_fishing_is_not_read_yet_is_marked_as_the_board_holds_them()
    {
        var pike = Find(RecordBoardViews.Build(Board(Pike, Holder(1, 95), Holder(ReaderId, 88)), Stranger, Show));

        Assert.Equal([false, true], pike.Top.Select(row => row.Mine));
        Assert.Equal(ReaderName, pike.Top[1].Name);
        Assert.Null(pike.Own);
    }

    [Fact]
    public void A_reader_who_never_caught_the_species_has_no_best_of_it()
    {
        var reader = Reader(FishSpecies.Perch, new FishRecord(30, 340, 1, Day));

        var view = RecordBoardViews.Build(Board(Pike, Holder(1, 95)), reader, Show);

        Assert.Null(Find(view).Own);
        var perch = view.Species.Single(species => species.Species == FishSpecies.Perch);
        var mine = Assert.Single(perch.Top);
        Assert.True(mine.Mine);
        Assert.Equal(1, mine.Rank);
    }

    [Fact]
    public void The_board_names_its_fields_as_the_page_reads_them()
    {
        var holders = Enumerable.Range(1, RecordBoardRules.TopCount).Select(id => Holder(id, 96 - id)).ToArray();
        var view = RecordBoardViews.Build(Board(Pike, holders), Reader(new FishRecord(40, 530, 1, Day)), Show);

        var root = JsonSerializer.SerializeToElement(view, FisherJson.Default.FisherRecordsView);

        var pike = root.GetProperty("species").EnumerateArray().Single(species => species.GetProperty("species").GetString() == "Pike");
        Assert.Equal("Pike", pike.GetProperty("name").GetString());
        var first = pike.GetProperty("top").EnumerateArray().First();
        Assert.Equal(1, first.GetProperty("rank").GetInt32());
        Assert.Equal("Angler 1", first.GetProperty("name").GetString());
        Assert.Equal(95, first.GetProperty("lengthCm").GetInt32());
        Assert.Equal(Grams(95), first.GetProperty("grams").GetInt32());
        Assert.Equal(Day.ToUnixTimeMilliseconds(), first.GetProperty("at").GetInt64());
        Assert.False(first.GetProperty("mine").GetBoolean());
        var own = pike.GetProperty("own");
        Assert.Equal(RecordBoardRules.TopCount + 1, own.GetProperty("rank").GetInt32());
        Assert.Equal(40, own.GetProperty("lengthCm").GetInt32());
        Assert.True(own.GetProperty("mine").GetBoolean());
        var perch = root.GetProperty("species").EnumerateArray().First();
        Assert.Equal(0, perch.GetProperty("top").GetArrayLength());
        Assert.Equal(JsonValueKind.Null, perch.GetProperty("own").ValueKind);
    }

    // The longest name of every angler, in letters that go out as escapes six bytes long, with five fish of every species
    // and the reader's own under each.
    [Theory]
    [InlineData('x')]
    [InlineData('ż')]
    [InlineData('中')]
    public void The_fullest_board_goes_to_its_page_in_one_message(char letter)
    {
        var name = new string(letter, ColumnLengths.NameKey);
        var board = new RecordBoard(FishCatalog.All.ToDictionary(fish => fish.Species, fish => new SpeciesBoard(
            [.. Enumerable.Range(1, RecordBoardRules.TopCount).Select(id => new RecordHolder(id, name, NamePublic: true, fish.LongestCm, fish.GramsAt(fish.LongestCm), Day))],
            [new FishLengthCount(fish.Species.ToString(), fish.LongestCm, Crowd)])));
        var reader = new RecordReader(ReaderId, name, FishCatalog.All.ToImmutableDictionary(
            fish => fish.Species, fish => new FishRecord(fish.ShortestCm, fish.GramsAt(fish.ShortestCm), 1, Day)));

        var view = RecordBoardViews.Build(board, reader, Show);
        var json = JsonSerializer.Serialize(view, FisherJson.Default.FisherRecordsView);

        Assert.All(view.Species, species => Assert.NotNull(species.Own));
        Assert.InRange(Encoding.UTF8.GetByteCount(json), 1, LoreJson.MaxAnswerBytes);
    }

    private static string Show(RecordHolder holder) => holder.Name;

    private static int Grams(int lengthCm) => FishCatalog.Find(Pike).GramsAt(lengthCm);

    private static RecordHolder Holder(long id, int lengthCm) => Holder(id, lengthCm, Day);

    private static RecordHolder Holder(long id, int lengthCm, DateTimeOffset at) =>
        new(id, $"Angler {id}", NamePublic: true, lengthCm, Grams(lengthCm), at);

    // Every holder counted once at its length, as the database counts them.
    private static RecordBoard Board(FishSpecies species, params RecordHolder[] holders) =>
        new(new Dictionary<FishSpecies, SpeciesBoard>
        {
            [species] = new(holders, [.. holders.GroupBy(holder => holder.LengthCm)
                .Select(group => new FishLengthCount(species.ToString(), group.Key, group.Count()))]),
        });

    private static RecordReader Reader() => new(ReaderId, ReaderName, ImmutableDictionary<FishSpecies, FishRecord>.Empty);

    private static RecordReader Reader(FishRecord pike) => Reader(Pike, pike);

    private static RecordReader Reader(FishSpecies species, FishRecord record) =>
        new(ReaderId, ReaderName, ImmutableDictionary<FishSpecies, FishRecord>.Empty.Add(species, record));

    private static FisherSpeciesView Find(FisherRecordsView view) => view.Species.Single(species => species.Species == Pike);
}
