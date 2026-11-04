using System.Numerics;
using Microsoft.Extensions.Logging;
using ReadyM.Api.Idents;
using ReadyM.Relay.Server.Sdk.Rpc;
using ReadyM.SDK.Attributes;
using Roleplay.Jobs.Common;

namespace Roleplay.Jobs.Server;

// The server end of MiningRpc: the generated sends, and the client's reports as events for MiningService, which this
// cannot depend on because it depends on this.
[RpcHandlersFor(typeof(MiningRpc))]
public sealed partial class MiningRpcHandlers(ILogger logger)
{
    public event Action<PlayerId>? Ready;
    public event Action<PlayerId, string, Vector3>? NodeHitReported;
    public event Action<PlayerId, string, Vector3, byte, string>? QuarryHitReported;
    public event Action<PlayerId, int>? PlaceRequested;

    partial void OnMiningReady(RpcContext context)
        => Raise(context, nameof(OnMiningReady), () => Ready?.Invoke(context.Sender));

    partial void OnNodeHit(RpcContext context, string id, float x, float y, float z)
        => Raise(context, nameof(OnNodeHit), () => NodeHitReported?.Invoke(context.Sender, id, new Vector3(x, y, z)));

    partial void OnQuarryHit(RpcContext context, string quarry, float x, float y, float z, byte struck, string mesh)
        => Raise(context, nameof(OnQuarryHit), () => QuarryHitReported?.Invoke(context.Sender, quarry, new Vector3(x, y, z), struck, mesh));

    partial void OnPlaceNode(RpcContext context, int kindIndex)
        => Raise(context, nameof(OnPlaceNode), () => PlaceRequested?.Invoke(context.Sender, kindIndex));

    private void Raise(RpcContext context, string message, Action raise)
    {
        try
        {
            raise();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Jobs: handling {Message} from {Player} failed.", message, context.Sender);
        }
    }
}
