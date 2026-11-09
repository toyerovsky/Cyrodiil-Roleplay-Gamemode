using Microsoft.Extensions.Logging;
using ReadyM.Api.Idents;
using ReadyM.Relay.Server.Sdk.Rpc;
using ReadyM.SDK.Attributes;
using Roleplay.Emotes.Common;

namespace Roleplay.Emotes.Server;

// The server end of EmotesRpc: the generated sends, and what the clients ask as events for Emotes, which this cannot
// depend on because it depends on this.
[RpcHandlersFor(typeof(EmotesRpc))]
public sealed partial class EmotesRpcHandlers(ILogger logger)
{
    public event Action<PlayerId>? Ready;
    public event Action<PlayerId, string>? Asked;
    public event Action<PlayerId>? StopAsked;

    partial void OnEmotesReady(RpcContext context)
        => Raise(context, nameof(OnEmotesReady), () => Ready?.Invoke(context.Sender));

    partial void OnAskEmote(RpcContext context, string id)
        => Raise(context, nameof(OnAskEmote), () => Asked?.Invoke(context.Sender, id));

    partial void OnAskStopEmote(RpcContext context)
        => Raise(context, nameof(OnAskStopEmote), () => StopAsked?.Invoke(context.Sender));

    private void Raise(RpcContext context, string message, Action raise)
    {
        try
        {
            raise();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Emotes: handling {Message} from {Player} failed.", message, context.Sender);
        }
    }
}
