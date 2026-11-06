using Microsoft.Extensions.Logging;
using ReadyM.SDK.Attributes;
using Roleplay.StatusTest.Common;

namespace Roleplay.StatusTest.Client;

// The client end of StatusTestRpc: SendRun is generated here, the server's report comes back as an event.
[RpcHandlersFor(typeof(StatusTestRpc))]
public sealed partial class StatusTestClientRpc(ILogger logger)
{
    public event Action<string>? Reported;

    partial void OnReport(string text)
    {
        try
        {
            Reported?.Invoke(text);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "StatusTest: showing the server's report failed.");
        }
    }
}
