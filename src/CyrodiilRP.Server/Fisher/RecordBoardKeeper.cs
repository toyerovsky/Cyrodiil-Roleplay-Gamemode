using CyrodiilRP.Server.Infrastructure;

namespace CyrodiilRP.Server.Fisher;

// Update thread only. Keeps the last reading of the records board with when it began: whoever asks within
// RecordBoardRules.KeptFor of that gets the same reading, done, still on its way or failed, so the database is read at
// most once in that time.
internal sealed class RecordBoardKeeper(IClock clock, Func<Task<RecordBoard>> read)
{
    private Task<RecordBoard>? _last;
    private long _beganMs;

    public Task<RecordBoard> ReadAsync()
    {
        var now = clock.MonotonicMs;
        if (_last is { } kept && now - _beganMs < RecordBoardRules.KeptFor.TotalMilliseconds)
            return kept;

        var reading = read();
        _beganMs = now;
        _last = reading;
        return reading;
    }
}
