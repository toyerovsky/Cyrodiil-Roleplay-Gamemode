using System.Numerics;
using Microsoft.Extensions.Logging;
using OblivionMP.SDK.Common.Foliage;
using ReadyM.Api.Idents;
using ReadyM.Relay.Server.Sdk.Rpc;
using ReadyM.SDK.Attributes;
using Roleplay.Jobs.Common;

namespace Roleplay.Jobs.Server;

// The server end of WoodcuttingRpc: the generated sends, and the client's reports as events for WoodcuttingService,
// which this cannot depend on because it depends on this.
[RpcHandlersFor(typeof(WoodcuttingRpc))]
public sealed partial class WoodcuttingRpcHandlers(ILogger logger)
{
    public event Action<PlayerId>? Ready;
    public event Action<PlayerId, FoliageId, float, Vector3, byte>? HitReported;

    partial void OnWoodcuttingReady(RpcContext context)
        => Raise(context, nameof(OnWoodcuttingReady), () => Ready?.Invoke(context.Sender));

    partial void OnTreeHit(RpcContext context, string mesh, int x, int y, int z, float scale, float hitX, float hitY, float hitZ, byte attack)
        => Raise(context, nameof(OnTreeHit),
            () => HitReported?.Invoke(context.Sender, new FoliageId(mesh, x, y, z), scale, new Vector3(hitX, hitY, hitZ), attack));

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
