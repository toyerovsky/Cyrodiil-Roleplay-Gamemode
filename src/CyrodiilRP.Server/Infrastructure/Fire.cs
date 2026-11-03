using Microsoft.Extensions.Logging;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Infrastructure;

[Service]
internal sealed partial class Fire(GameThread gameThread, ILogger logger)
{
    public void Run(string what, Func<Task> work)
    {
        Task task;
        using (gameThread.Enter(what))
        {
            try
            {
                task = work();
            }
            catch (Exception exception)
            {
                Report(exception, what);
                return;
            }
        }

        task.ContinueWith(done =>
        {
            if (done.Exception is { } failure)
                Report(failure.GetBaseException(), what);
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    private void Report(Exception exception, string what)
    {
        if (exception is OperationCanceledException)
            return;

        logger.LogError(exception, "[CyrodiilRP] {Work} failed.", what);
    }
}
