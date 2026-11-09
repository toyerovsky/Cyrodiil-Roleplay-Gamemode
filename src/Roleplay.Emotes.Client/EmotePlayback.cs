using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OblivionMP.SDK;
using ReadyM.SDK.Attributes;

namespace Roleplay.Emotes.Client;

// Plays what the server says: this player's emotes through IAnimations.Play, others' sequences through PlayOn on
// their character as this client draws it. Tells the server each time the character comes into the world.
[Service]
public sealed partial class EmotePlayback(EmotesClientRpc rpc, IAnimations animations, IPlayers players, ILogger logger) : ILocalEmotes
{
    private const float BlendOutSeconds = 0.25f;

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private bool _inWorld;
    private double _stopAt = double.PositiveInfinity;

    private double Now => _clock.Elapsed.TotalSeconds;

    public void Ask(string id) => rpc.SendAskEmote(id);

    public void AskStop() => rpc.SendAskStopEmote();

    private void Start()
    {
        rpc.Played += OnPlayed;
        rpc.PlayedOn += OnPlayedOn;
        rpc.Stopped += StopNow;
    }

    private void Stop()
    {
        rpc.Played -= OnPlayed;
        rpc.PlayedOn -= OnPlayedOn;
        rpc.Stopped -= StopNow;
    }

    private void Update()
    {
        var inWorld = players.LocalMainCharacter is not null;
        if (inWorld && !_inWorld)
            rpc.SendEmotesReady();
        _inWorld = inWorld;

        if (Now >= _stopAt)
            StopNow();
    }

    private void OnPlayed(EmoteOrder order)
    {
        var seconds = animations.Play(order.Animation, order.FirstPersonAnimation, order.PlayRate);
        if (seconds <= 0f)
        {
            logger.LogWarning("Emotes: {Animation} did not play, no character or no such animation.", order.Animation);
            _stopAt = double.PositiveInfinity;
            return;
        }

        _stopAt = order.Seconds > 0f ? Now + order.Seconds : double.PositiveInfinity;
    }

    // Not drawn here (another area, too far) is the usual case, not a fault
    private void OnPlayedOn(OthersEmote emote)
    {
        foreach (var character in players.AreaPlayers)
        {
            if (character.PlayerId.RawValue == emote.Player)
            {
                animations.PlayOn(character, emote.Animation, emote.PlayRate);
                return;
            }
        }
    }

    private void StopNow()
    {
        _stopAt = double.PositiveInfinity;
        animations.Stop(BlendOutSeconds);
    }
}
