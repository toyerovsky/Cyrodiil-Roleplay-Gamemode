using Microsoft.Extensions.Logging;
using ReadyM.SDK.Attributes;
using Roleplay.Emotes.Common;

namespace Roleplay.Emotes.Client;

// The client end of EmotesRpc: the sends are generated here, the server's messages go out as events for
// EmotePlayback, which this cannot depend on because it depends on this.
[RpcHandlersFor(typeof(EmotesRpc))]
public sealed partial class EmotesClientRpc(ILogger logger)
{
    public event Action<EmoteOrder>? Played;
    public event Action<OthersEmote>? PlayedOn;
    public event Action? Stopped;

    partial void OnPlayEmote(string animation, string firstPersonAnimation, float playRate, float seconds)
        => Raise(nameof(OnPlayEmote), () => Played?.Invoke(
            new EmoteOrder(animation, firstPersonAnimation.Length > 0 ? firstPersonAnimation : null, playRate, seconds)));

    partial void OnPlayEmoteOn(int player, string animation, float playRate)
        => Raise(nameof(OnPlayEmoteOn), () => PlayedOn?.Invoke(new OthersEmote(player, animation, playRate)));

    partial void OnStopEmote() => Raise(nameof(OnStopEmote), () => Stopped?.Invoke());

    private void Raise(string message, Action raise)
    {
        try
        {
            raise();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Emotes: handling {Message} failed.", message);
        }
    }
}
