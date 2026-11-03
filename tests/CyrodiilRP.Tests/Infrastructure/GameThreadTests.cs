using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Tests.Fakes;
using Microsoft.Extensions.Logging;
using ReadyM.SDK.Services;

namespace CyrodiilRP.Tests.Infrastructure;

public sealed class GameThreadTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    // Past the watchdog's 5 ms.
    private static readonly TimeSpan SlowWork = TimeSpan.FromMilliseconds(20);

    private readonly ListLogger _log = new();
    private readonly GameThread _thread;
    private readonly Fire _fire;
    private bool _inTick;

    public GameThreadTests()
    {
        _thread = new GameThread(new Watchdog(new SystemClock(), _log));
        _fire = new Fire(_thread, _log);
    }

    private void Tick()
    {
        _inTick = true;
        ((IUpdatingService)_thread).Update(default);
        _inTick = false;
    }

    [Fact]
    public void Work_posted_during_a_tick_waits_for_the_next_one()
    {
        var ran = new List<string>();
        _thread.Post("outer", () =>
        {
            ran.Add("outer");
            _thread.Post("inner", () => ran.Add("inner"));
        });

        Tick();
        Assert.Equal(["outer"], ran);

        Tick();
        Assert.Equal(["outer", "inner"], ran);
    }

    [Fact]
    public void Work_that_throws_is_logged_under_its_name_and_the_rest_still_runs()
    {
        var ran = false;
        _thread.Post("test.broken", () => throw new InvalidOperationException("broken"));
        _thread.Post("test.fine", () => ran = true);

        Tick();

        Assert.True(ran);
        Assert.Contains(_log.Errors, line => line.Text.Contains("test.broken", StringComparison.Ordinal));
    }

    [Fact]
    public async Task An_await_started_by_fire_comes_back_on_a_tick()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool? resumedInTick = null;
        _fire.Run("test.await", async () =>
        {
            await gate.Task;
            resumedInTick = _inTick;
        });

        await Task.Run(gate.SetResult);
        await Task.Delay(50);
        Assert.Null(resumedInTick);

        await TickUntil(() => resumedInTick is not null);
        Assert.True(resumedInTick);
    }

    [Fact]
    public async Task What_fired_work_throws_after_an_await_is_logged_and_a_cancel_is_not()
    {
        _fire.Run("test.failing", async () =>
        {
            await Task.Yield();
            throw new InvalidOperationException("broken");
        });
        _fire.Run("test.cancelled", async () =>
        {
            await Task.Yield();
            throw new OperationCanceledException();
        });

        await TickUntil(() => _log.Errors.Any());
        await Task.Delay(100);
        Tick();

        var error = Assert.Single(_log.Errors);
        Assert.Contains("test.failing", error.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Every_await_of_fired_work_comes_back_under_its_name()
    {
        _fire.Run("test.slow", async () =>
        {
            await Task.Yield();
            await Task.Yield();
            Thread.Sleep(SlowWork);
        });

        await TickUntil(() => _log.Lines.Any(line => line.Level == LogLevel.Warning));

        Assert.Contains("[slow] await test.slow took", Assert.Single(_log.Lines, line => line.Level == LogLevel.Warning).Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Our_context_is_put_back_after_our_work()
    {
        var before = SynchronizationContext.Current;
        SynchronizationContext? during = null;
        _thread.Post("test.context", () => during = SynchronizationContext.Current);

        Tick();

        Assert.Same(_thread.Context, during);
        Assert.Same(before, SynchronizationContext.Current);
    }

    // As the server's update loop would.
    private async Task TickUntil(Func<bool> done)
    {
        var until = DateTime.UtcNow + Patience;
        while (!done())
        {
            Assert.True(DateTime.UtcNow < until, "Timed out.");
            Tick();
            await Task.Delay(10);
        }
    }
}
