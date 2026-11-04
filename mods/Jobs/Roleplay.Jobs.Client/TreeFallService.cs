using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OblivionMp.Sdk;
using OblivionMp.Sdk.Common.Foliage;
using OblivionMp.Sdk.Common.Props;
using ReadyM.SDK.Attributes;
using Roleplay.Jobs.Common;

namespace Roleplay.Jobs.Client;

/// <summary>
/// Plays a felled tree coming down, on this client alone: a copy of it, with nothing colliding with it, takes its place,
/// tips over away from the chopper, lies there, then sinks through the ground and is gone (<see cref="TreeFall"/>). The
/// tree itself is already hidden by then. Only for a tree felled while this player is near it: one that came down
/// before they joined or came close is just gone.
/// </summary>
[Service]
public sealed partial class TreeFallService(
    IPlayers players,
    IFoliage foliage,
    ILocalProps localProps,
    WoodcuttingClientRpc rpc,
    ILogger logger)
{
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly List<FallingTree> _falling = [];
    private TreeFall? _fall;

    private sealed record FallingTree(FoliageId Tree, LocalPropHandle Copy, FoliageTransform Standing, float FallYaw, TreeFall Fall, double FelledAt);

    private void Start()
    {
        rpc.RulesReceived += OnRules;
        rpc.TreeFelled += OnTreeFelled;
    }

    private void Stop()
    {
        rpc.RulesReceived -= OnRules;
        rpc.TreeFelled -= OnTreeFelled;
        foreach (var falling in _falling)
            localProps.Remove(falling.Copy);
        _falling.Clear();
    }

    private void Update()
    {
        if (_falling.Count == 0)
            return;

        var now = _clock.Elapsed.TotalSeconds;
        for (var i = _falling.Count - 1; i >= 0; i--)
        {
            var falling = _falling[i];
            var seconds = (float)(now - falling.FelledAt);
            // The rules can change with a reconnect while a tree is down; it finishes as it started
            var fall = falling.Fall;
            if (seconds >= fall.SecondsFor(falling.Standing.Scale))
            {
                localProps.Remove(falling.Copy);
                _falling.RemoveAt(i);
                continue;
            }

            var (tilt, sink) = fall.PoseAt(seconds, falling.Standing.Scale);
            localProps.Move(falling.Copy, falling.Standing.Position with { Z = falling.Standing.Position.Z - sink },
                TreeFall.Tipped(falling.Standing.Rotation, falling.FallYaw, tilt));
        }
    }

    private void OnRules(TreeFilter filter, float reach, float facingDegrees, TreeFall? fall) => _fall = fall;

    private void OnTreeFelled(FoliageId tree, string kind, float scale, bool byYou, float fallYaw)
    {
        if (_fall is not { } fall || players.LocalMainCharacter is not { } character)
            return;
        if (ChopGeometry.HorizontalDistance(character.Position, tree.Position) > fall.ViewDistance)
            return;

        if (!foliage.TryGetTransform(tree, out var standing))
        {
            logger.LogInformation("Jobs: {Tree} came down out of sight, nothing to fall.", tree);
            return;
        }

        var copy = localProps.Place(new Prop(tree.Mesh, standing.Position, standing.Rotation, standing.Scale), collides: false);
        _falling.Add(new FallingTree(tree, copy, standing, fallYaw, fall, _clock.Elapsed.TotalSeconds));
        logger.LogInformation("Jobs: {Tree} falls towards {Yaw:0} degrees.", tree, fallYaw);
    }
}
