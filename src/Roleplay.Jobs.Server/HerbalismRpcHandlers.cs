using Microsoft.Extensions.Logging;
using ReadyM.Api.Idents;
using ReadyM.Relay.Server.Sdk.Rpc;
using ReadyM.SDK.Attributes;
using Roleplay.Jobs.Common;

namespace Roleplay.Jobs.Server;

// The server end of HerbsRpc: the generated sends, and the client's report as an event for HerbalismService, which this
// cannot depend on because it depends on this.
[RpcHandlersFor(typeof(HerbsRpc))]
public sealed partial class HerbalismRpcHandlers(ILogger logger)
{
    public event Action<PlayerId>? Ready;

    partial void OnHerbsReady(RpcContext context)
    {
        try
        {
            Ready?.Invoke(context.Sender);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Jobs: handling HerbsReady from {Player} failed.", context.Sender);
        }
    }
}
