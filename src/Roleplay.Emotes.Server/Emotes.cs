using LiteNetLib;
using Microsoft.Extensions.Logging;
using ReadyM.Api.Idents;
using ReadyM.SDK.Archetypes.Core;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Server.Events;
using Roleplay.Emotes.Common;

namespace Roleplay.Emotes.Server;

// IEmotes, offered to the other mods by Mod.Init. A client asking by id goes through Play too, so an emote taken off
// the list cannot be played from a list that still showed it.
[Service]
public sealed partial class Emotes(IServerEvents serverEvents, EmotesRpcHandlers rpc, ILogger logger) : IEmotes
{
    private readonly EmoteRegistry _registry = CreateRegistry();

    // Clients whose character is in the world: they are sent their own emotes and the sequences of others
    private readonly HashSet<PlayerId> _ready = [];

    public IReadOnlyList<Emote> All => _registry.All;

    public void Register(Emote emote) => _registry.Add(emote);

    public bool Unregister(string id) => _registry.Remove(id);

    public EmoteRefusal? Play(PlayerId player, string id)
    {
        if (_registry.Find(id) is not { } emote)
            return EmoteRefusal.Unknown;
        if (!_ready.Contains(player))
            return EmoteRefusal.NotInWorld;

        rpc.SendPlayEmote(player, emote.Animation, emote.FirstPersonAnimation ?? "", emote.PlayRate, emote.Seconds ?? 0f);
        switch (emote.Kind)
        {
            case AnimationKind.Montage:
                break;
            case AnimationKind.Sequence:
                foreach (var other in _ready.Where(other => other != player))
                    rpc.SendPlayEmoteOn(other, player.RawValue, emote.Animation, emote.PlayRate);
                break;
            default:
                throw new InvalidOperationException($"The emote {emote.Id} has an animation of kind {emote.Kind}.");
        }

        return null;
    }

    public void Stop(PlayerId player)
    {
        if (_ready.Contains(player))
            rpc.SendStopEmote(player);
    }

    private static EmoteRegistry CreateRegistry()
    {
        var registry = new EmoteRegistry();
        foreach (var emote in VanillaEmotes.All)
            registry.Add(emote);
        return registry;
    }

    private void Start()
    {
        rpc.Ready += OnReady;
        rpc.Asked += OnAsked;
        rpc.StopAsked += Stop;
        serverEvents.OnPlayerDisconnected += OnDisconnected;
    }

    private void Stop()
    {
        rpc.Ready -= OnReady;
        rpc.Asked -= OnAsked;
        rpc.StopAsked -= Stop;
        serverEvents.OnPlayerDisconnected -= OnDisconnected;
    }

    private void OnReady(PlayerId player) => _ready.Add(player);

    // Asked by another client mod, which showed the list itself: a refusal is that mod's to prevent, so it is logged
    private void OnAsked(PlayerId player, string id)
    {
        if (Play(player, id) is { } refusal)
            logger.LogWarning("Emotes: {Player} asked for the emote {Id}, refused: {Refusal}.", player, id, refusal);
    }

    private void OnDisconnected(Player scope, Guid session, DisconnectReason reason) => _ready.Remove(scope.PlayerId);
}
