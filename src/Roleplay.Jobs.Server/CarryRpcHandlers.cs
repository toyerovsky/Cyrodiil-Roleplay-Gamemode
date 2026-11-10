using System.Numerics;
using Microsoft.Extensions.Logging;
using ReadyM.Api.Idents;
using ReadyM.Relay.Server.Sdk.Rpc;
using ReadyM.SDK.Attributes;
using Roleplay.Jobs.Common;

namespace Roleplay.Jobs.Server;

// The server end of CarryRpc: the generated sends, and the client's requests as events for CarryService, which this
// cannot depend on because it depends on this.
[RpcHandlersFor(typeof(CarryRpc))]
public sealed partial class CarryRpcHandlers(ILogger logger)
{
    public event Action<PlayerId>? Ready;
    public event Action<PlayerId>? KeyPressed;
    public event Action<PlayerId, Vector3, float>? PutDownRequested;
    public event Action<PlayerId, int, Vector3, float>? PlaceRequested;

    partial void OnCarryReady(RpcContext context)
        => Raise(context, nameof(OnCarryReady), () => Ready?.Invoke(context.Sender));

    partial void OnCarryKey(RpcContext context)
        => Raise(context, nameof(OnCarryKey), () => KeyPressed?.Invoke(context.Sender));

    partial void OnPutDown(RpcContext context, float x, float y, float z, float yaw)
        => Raise(context, nameof(OnPutDown), () => PutDownRequested?.Invoke(context.Sender, new Vector3(x, y, z), yaw));

    partial void OnPlaceCarryable(RpcContext context, int kindIndex, float x, float y, float z, float yaw)
        => Raise(context, nameof(OnPlaceCarryable), () => PlaceRequested?.Invoke(context.Sender, kindIndex, new Vector3(x, y, z), yaw));

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
