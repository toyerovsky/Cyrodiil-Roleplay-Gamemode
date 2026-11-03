using CyrodiilRP.Server.Players;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Infrastructure;

// The host gives mods no shutdown, so play time is written once a minute and the process exit writes what is queued.
[Service]
internal sealed partial class ServerLifecycle(OnlinePlayers online, Audit audit, Persistence persistence, Timers timers)
{
    private const string PlayTimeJob = "players.play_time";

    private static readonly TimeSpan PlayTimeEvery = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan ExitDrain = TimeSpan.FromSeconds(5);

    private int _stopped;

    private void Start()
    {
        timers.RunEvery(PlayTimeJob, PlayTimeEvery, online.RecordPlayTime);
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
    }

    private void Stop() => ShutDown();

    private void OnProcessExit(object? sender, EventArgs args) => ShutDown();

    private void ShutDown()
    {
        if (Interlocked.Exchange(ref _stopped, 1) == 1)
            return;

        audit.FlushNow();
        persistence.Writer.DrainAsync(ExitDrain).GetAwaiter().GetResult();
    }
}
