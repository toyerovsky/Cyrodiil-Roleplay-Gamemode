using CyrodiilRP.Data.Fishing;
using CyrodiilRP.Server.Fisher;
using CyrodiilRP.Server.Fishing;
using CyrodiilRP.Tests.Fakes;

namespace CyrodiilRP.Tests.Fisher;

public sealed class FisherRecordBoardTests
{
    private const string Pike = nameof(FishSpecies.Pike);
    private const string Perch = nameof(FishSpecies.Perch);
    private const string Unknown = "Kraken";

    private static readonly DateTimeOffset Day = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);

    private readonly ListLogger _logger = new();

    [Fact]
    public void The_rows_of_the_database_become_a_board_of_each_species()
    {
        var stored = new FishBoard(
            [Row(Perch, 3, 30), Row(Pike, 1, 90), Row(Pike, 2, 74)],
            [new(Perch, 30, 1), new(Pike, 90, 1), new(Pike, 74, 2), new(Pike, 60, 5)]);

        var board = RecordBoard.Create(stored, _logger);

        var pike = board.Find(FishSpecies.Pike);
        Assert.Equal([1L, 2L], pike.Top.Select(holder => holder.CharacterId));
        var first = pike.Top[0];
        Assert.Equal("First1 Last1", first.Name);
        Assert.True(first.NamePublic);
        Assert.Equal(90, first.LengthCm);
        Assert.Equal(900, first.Grams);
        Assert.Equal(Day, first.At);
        Assert.Equal(0, pike.CountLonger(90));
        Assert.Equal(1, pike.CountLonger(74));
        Assert.Equal(3, pike.CountLonger(60));
        Assert.Equal(8, pike.CountLonger(59));
        Assert.Equal(3L, Assert.Single(board.Find(FishSpecies.Perch).Top).CharacterId);
        Assert.Empty(_logger.Errors);
    }

    [Fact]
    public void A_species_nobody_caught_is_an_empty_board()
    {
        var board = RecordBoard.Create(new FishBoard([], []), _logger);

        Assert.All(Enum.GetValues<FishSpecies>(), species =>
        {
            Assert.Empty(board.Find(species).Top);
            Assert.Equal(0, board.Find(species).CountLonger(0));
        });
    }

    [Fact]
    public void A_species_the_mode_does_not_know_is_left_out_and_told_as_an_error()
    {
        var stored = new FishBoard([Row(Unknown, 1, 500), Row(Pike, 2, 74)], [new(Unknown, 500, 1), new(Pike, 74, 1)]);

        var board = RecordBoard.Create(stored, _logger);

        Assert.Equal([FishSpecies.Pike], board.Species.Keys);
        Assert.Contains(Unknown, Assert.Single(_logger.Errors).Text);
    }

    private static FishBoardRow Row(string species, long characterId, int lengthCm) =>
        new(species, characterId, $"First{characterId}", $"Last{characterId}", NamePublic: true, lengthCm, BestGrams: lengthCm * 10, Day);
}
