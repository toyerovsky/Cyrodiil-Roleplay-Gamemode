using CyrodiilRP.Server.Fisher;
using CyrodiilRP.Server.Fishing;
using CyrodiilRP.Tests.Fakes;

namespace CyrodiilRP.Tests.Fisher;

public sealed class FisherRecordBoardKeeperTests
{
    private static readonly TimeSpan Moment = TimeSpan.FromSeconds(1);

    private readonly FakeClock _clock = new();
    private readonly List<TaskCompletionSource<RecordBoard>> _readings = [];
    private readonly RecordBoardKeeper _keeper;

    public FisherRecordBoardKeeperTests()
    {
        _keeper = new RecordBoardKeeper(_clock, () =>
        {
            _readings.Add(new TaskCompletionSource<RecordBoard>());
            return _readings[^1].Task;
        });
    }

    [Fact]
    public async Task One_reading_serves_everyone_who_asks_within_a_minute()
    {
        var board = Board();
        var first = _keeper.ReadAsync();
        _readings[0].SetResult(board);

        _clock.Advance(RecordBoardRules.KeptFor - Moment);
        var second = _keeper.ReadAsync();

        Assert.Single(_readings);
        Assert.Same(board, await first);
        Assert.Same(board, await second);
    }

    [Fact]
    public async Task The_board_is_read_again_once_the_last_reading_is_a_minute_old()
    {
        var old = Board();
        var fresh = Board();
        _ = _keeper.ReadAsync();
        _readings[0].SetResult(old);

        _clock.Advance(RecordBoardRules.KeptFor);
        var later = _keeper.ReadAsync();
        _readings[1].SetResult(fresh);
        _clock.Advance(Moment);
        var after = _keeper.ReadAsync();

        Assert.Equal(2, _readings.Count);
        Assert.Same(fresh, await later);
        Assert.Same(fresh, await after);
    }

    [Fact]
    public async Task Those_who_ask_while_the_database_is_being_read_wait_for_the_same_reading()
    {
        var board = Board();
        var first = _keeper.ReadAsync();
        _clock.Advance(Moment);
        var second = _keeper.ReadAsync();
        Assert.False(first.IsCompleted);

        _readings[0].SetResult(board);

        Assert.Single(_readings);
        Assert.Same(board, await first);
        Assert.Same(board, await second);
    }

    [Fact]
    public async Task A_reading_that_failed_is_not_tried_again_within_the_minute_and_is_after_it()
    {
        var first = _keeper.ReadAsync();
        _readings[0].SetException(new InvalidOperationException("The database is away."));
        _clock.Advance(Moment);
        var second = _keeper.ReadAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => first);
        await Assert.ThrowsAsync<InvalidOperationException>(() => second);
        Assert.Single(_readings);

        _clock.Advance(RecordBoardRules.KeptFor);
        var board = Board();
        var third = _keeper.ReadAsync();
        _readings[1].SetResult(board);

        Assert.Same(board, await third);
    }

    private static RecordBoard Board() => new(new Dictionary<FishSpecies, SpeciesBoard>());
}
