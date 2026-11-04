using System.Numerics;
using Microsoft.Extensions.Logging;
using OblivionMp.Sdk.Common.Foliage;
using ReadyM.Api.Idents;
using ReadyM.Relay.Server.Sdk.Rpc;
using ReadyM.SDK.Attributes;
using Roleplay.Jobs.Common;

namespace Roleplay.Jobs.Server;

// The server end of WoodcuttingRpc: the generated sends, and the client's requests as events for WoodcuttingService,
// which this cannot depend on because it depends on this.
[RpcHandlersFor(typeof(WoodcuttingRpc))]
public sealed partial class WoodcuttingRpcHandlers(ILogger logger)
{
    public event Action<PlayerId>? Ready;
    public event Action<PlayerId, FoliageId, float>? StartRequested;
    public event Action<PlayerId, Vector3>? HitReported;
    public event Action<PlayerId>? CancelRequested;

    partial void OnWoodcuttingReady(RpcContext context)
        => Raise(context, nameof(OnWoodcuttingReady), () => Ready?.Invoke(context.Sender));

    partial void OnStartChop(RpcContext context, string mesh, int x, int y, int z, float scale)
        => Raise(context, nameof(OnStartChop), () => StartRequested?.Invoke(context.Sender, new FoliageId(mesh, x, y, z), scale));

    partial void OnChopHit(RpcContext context, float x, float y, float z)
        => Raise(context, nameof(OnChopHit), () => HitReported?.Invoke(context.Sender, new Vector3(x, y, z)));

    partial void OnCancelChop(RpcContext context)
        => Raise(context, nameof(OnCancelChop), () => CancelRequested?.Invoke(context.Sender));

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
