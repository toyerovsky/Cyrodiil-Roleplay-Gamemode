using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Tests.Fakes;

namespace CyrodiilRP.Tests.Infrastructure;

public sealed class CallbacksTests
{
    private readonly ListLogger _log = new();
    private readonly Callbacks _callbacks;

    public CallbacksTests()
    {
        var watchdog = new Watchdog(new SystemClock(), _log);
        _callbacks = new Callbacks(new GameThread(watchdog), watchdog);
    }

    [Fact]
    public void A_handler_that_throws_is_logged_under_the_event_and_its_class_and_the_others_still_run()
    {
        var seen = new List<int>();
        Action<int>? handlers = null;
        handlers += seen.Add;
        handlers += Throw;
        handlers += value => seen.Add(value * 10);

        _callbacks.Raise("test.event", handlers, 4);

        Assert.Equal([4, 40], seen);
        var error = Assert.Single(_log.Errors);
        Assert.Contains("test.event CallbacksTests failed", error.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_lambda_is_logged_under_the_class_that_wrote_it()
    {
        var broken = "broken";
        Action<int>? handlers = null;
        handlers += value => throw new InvalidOperationException($"{broken} by {value}");

        _callbacks.Raise("test.event", handlers, 1);

        Assert.Contains("test.event CallbacksTests failed", Assert.Single(_log.Errors).Text, StringComparison.Ordinal);
    }

    [Fact]
    public void No_handlers_is_nothing_to_do()
    {
        _callbacks.Raise<int>("test.event", null, 1);

        Assert.Empty(_log.Lines);
    }

    private static void Throw(int value) => throw new InvalidOperationException($"broken by {value}");
}
