using System.Diagnostics;
using System.Numerics;
using OblivionMP.SDK;
using OblivionMP.SDK.Common.Archetypes;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Client.Entities;
using Roleplay.Jobs.Common;

namespace Roleplay.Jobs.Client;

/// <summary>
/// The crosshair names a foreman after the person they are made from, and the platform renames by base form alone, in
/// this player's game. So the base is named after the foreman the character faces, by role, and gets the game's name
/// back when it faces none. People of the same base that another mod stands (a gamemode's traders) are that mod's to
/// name: a nearer one of theirs leaves the name to them.
/// </summary>
[Service]
public sealed partial class ForemanCrosshair(IPlayers players, IEntities entities, ICrosshair crosshair, ForemanLabels labels)
{
    private const double LookEverySeconds = 0.1;

    // Past the reach of the game's own crosshair, so the name is there before the crosshair shows it
    private const float Reach = 600f;
    private const float ConeDegrees = 60f;

    private readonly record struct Faced(uint BaseForm, string Label, float Distance);

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private double _nextLook;

    // The base form that carries a role's name now
    private uint? _renamed;

    private void Stop() => Restore();

    private void Update()
    {
        if (_clock.Elapsed.TotalSeconds < _nextLook)
            return;
        _nextLook = _clock.Elapsed.TotalSeconds + LookEverySeconds;

        if (FindFaced() is not { } faced)
        {
            Restore();
            return;
        }

        if (_renamed != faced.BaseForm)
            Restore();

        // Every look, not once: another mod may have named the base in between for one of its own
        crosshair.Rename(faced.BaseForm, faced.Label);
        _renamed = faced.BaseForm;
    }

    private void Restore()
    {
        if (_renamed is not { } baseForm)
            return;
        crosshair.Restore(baseForm);
        _renamed = null;
    }

    private Faced? FindFaced()
    {
        if (players.LocalMainCharacter is not { } character)
            return null;

        var origin = character.Position;
        var yaw = character.Rotation;

        Faced? ours = null;
        foreach (var npc in entities.Query<Npc>())
        {
            if (labels.Find(npc.SpawnerRefId) is not { } label || !Facing.IsAhead(origin, yaw, npc.Position, Reach, ConeDegrees))
                continue;

            var distance = Vector3.Distance(origin, npc.Position);
            if (ours is null || distance < ours.Value.Distance)
                ours = new Faced(npc.BaseId.FormId.Value, label, distance);
        }

        if (ours is not { } faced)
            return null;

        foreach (var npc in entities.Query<Npc>())
        {
            if (npc.BaseId.FormId.Value == faced.BaseForm && labels.Find(npc.SpawnerRefId) is null
                && Facing.IsAhead(origin, yaw, npc.Position, Reach, ConeDegrees)
                && Vector3.Distance(origin, npc.Position) < faced.Distance)
                return null;
        }

        return faced;
    }
}
