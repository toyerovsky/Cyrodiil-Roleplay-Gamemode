using System.Numerics;
using CyrodiilRP.Common.Players;
using OblivionMP.SDK;
using OblivionMP.SDK.Common.Archetypes;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Client.Entities;

namespace CyrodiilRP.Client.Traders;

// The crosshair names a person after the base form they are made from, and the platform renames by base form alone, in
// this player's game. Every person of the mode is made from one base, so that base is named after the one the
// character faces, and gets the game's name back when it faces none. People of the same base that another mod stands
// (the Jobs foremen) are that mod's to name: a nearer one of theirs leaves the name to them.
[Service]
internal sealed partial class TraderCrosshair(IPlayers players, IEntities entities, ICrosshair crosshair, TraderLabelRegistry labels)
{
    private const float LookEverySeconds = 0.1f;

    // Past the reach of the game's own crosshair, so the name is there before the crosshair shows it.
    private const float Reach = 600f;
    private const float ConeDegrees = 60f;

    // The base form that carries a name of the mode now.
    private uint? _renamed;

    private float _lastLook = float.NegativeInfinity;

    private readonly record struct Faced(uint BaseForm, string Label, float Distance);

    private void Stop() => Restore();

    private void Update()
    {
        if (Time.Elapsed - _lastLook < LookEverySeconds)
            return;

        _lastLook = Time.Elapsed;
        if (FindFaced() is not { } faced)
        {
            Restore();
            return;
        }

        if (_renamed != faced.BaseForm)
            Restore();

        // Every look, not once: another mod may have named the base in between for one of its own.
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
        if (labels.IsEmpty || players.LocalMainCharacter is not { } character)
            return null;

        var origin = character.Position;
        var yaw = character.Rotation;

        Faced? ours = null;
        foreach (var npc in entities.Query<Npc>())
        {
            if (labels.Find(npc.SpawnerRefId) is not { } label || !FrontCone.Contains(origin, yaw, npc.Position, Reach, ConeDegrees))
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
                && FrontCone.Contains(origin, yaw, npc.Position, Reach, ConeDegrees)
                && Vector3.Distance(origin, npc.Position) < faced.Distance)
                return null;
        }

        return faced;
    }
}
