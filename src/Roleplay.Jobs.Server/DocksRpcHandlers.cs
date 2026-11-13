using System.Numerics;
using Microsoft.Extensions.Logging;
using ReadyM.Api.Idents;
using ReadyM.Relay.Server.Sdk.Rpc;
using ReadyM.SDK.Attributes;
using Roleplay.Jobs.Common;

namespace Roleplay.Jobs.Server;

// The server end of DocksRpc: the generated sends, and the client's requests as events for DockService, which this
// cannot depend on because it depends on this.
[RpcHandlersFor(typeof(DocksRpc))]
public sealed partial class DocksRpcHandlers(ILogger logger)
{
    public event Action<PlayerId>? Ready;
    public event Action<PlayerId, int, Vector3, float>? ForemanPlaceRequested;
    public event Action<PlayerId, DockZoneKind, Vector3, float>? ZonePlaceRequested;
    public event Action<PlayerId>? RemoveRequested;
    public event Action<PlayerId, Vector3, float>? CrateMeasured;
    public event Action<PlayerId, Vector3, Vector3>? BoardMeasured;

    partial void OnDocksReady(RpcContext context)
        => Raise(context, nameof(OnDocksReady), () => Ready?.Invoke(context.Sender));

    partial void OnPlaceForeman(RpcContext context, int roleIndex, float x, float y, float z, float yaw)
        => Raise(context, nameof(OnPlaceForeman), () => ForemanPlaceRequested?.Invoke(context.Sender, roleIndex, new Vector3(x, y, z), yaw));

    partial void OnPlaceZone(RpcContext context, byte kind, float x, float y, float z, float yaw)
    {
        if (!Enum.IsDefined((DockZoneKind)kind))
        {
            logger.LogWarning("Jobs: {Player} asked for a deposit of kind {Kind}, which is none. Ignored.", context.Sender, kind);
            return;
        }
        Raise(context, nameof(OnPlaceZone), () => ZonePlaceRequested?.Invoke(context.Sender, (DockZoneKind)kind, new Vector3(x, y, z), yaw));
    }

    partial void OnCrateMeasured(RpcContext context, float x, float y, float z, float bottom)
        => Raise(context, nameof(OnCrateMeasured), () => CrateMeasured?.Invoke(context.Sender, new Vector3(x, y, z), bottom));

    partial void OnBoardMeasured(RpcContext context, float minX, float minY, float minZ, float maxX, float maxY, float maxZ)
        => Raise(context, nameof(OnBoardMeasured),
            () => BoardMeasured?.Invoke(context.Sender, new Vector3(minX, minY, minZ), new Vector3(maxX, maxY, maxZ)));

    partial void OnRemoveNearest(RpcContext context)
        => Raise(context, nameof(OnRemoveNearest), () => RemoveRequested?.Invoke(context.Sender));

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
