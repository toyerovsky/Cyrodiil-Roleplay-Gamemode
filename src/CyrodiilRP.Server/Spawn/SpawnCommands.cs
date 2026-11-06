using System.Globalization;
using CyrodiilRP.Data.Staff;
using CyrodiilRP.Server.Admin;
using CyrodiilRP.Server.Commands;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Localization;
using OblivionMP.SDK.Common.Archetypes;
using ReadyM.SDK.Archetypes.Core;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Server.Entities;

namespace CyrodiilRP.Server.Spawn;

[Service]
internal sealed partial class SpawnCommands(CommandRegistry commands, IEntities entities, SpawnPoints spawn, Teleports teleports, Audit audit, Fire fire)
{
    private const string SetAction = "spawn.place";
    private const string Here = "here";
    private const string Go = "go";
    private const string Destination = "spawn";

    private void Start() => commands.AddStaff(StaffPermissions.Spawn, SpawnText.CmdSpawn, Run, needsCharacter: true);

    private void Run(CommandCall call)
    {
        if (call.Words.Count == 0)
        {
            Show(call);
            return;
        }

        switch (call.Words[0])
        {
            case Here:
                Set(call);
                break;

            case Go:
                GoThere(call);
                break;

            default:
                call.Refuse(SpawnText.CmdSpawn);
                break;
        }
    }

    private void Show(CommandCall call)
    {
        if (spawn.Current is not { } place)
        {
            call.Reply(SpawnText.None);
            return;
        }

        var position = place.Position;
        call.Reply(SpawnText.At(
            position: string.Create(CultureInfo.InvariantCulture, $"{MathF.Round(position.X)} {MathF.Round(position.Y)} {MathF.Round(position.Z)}"),
            area: GameSpace.Outside.ToString()));
    }

    private void Set(CommandCall call)
    {
        if (!entities.TryLookup(call.Sender.Id, out MainCharacter main) || !entities.TryGetScope(main, out Area area))
        {
            call.Refuse(SpawnText.NotInWorld);
            return;
        }

        if (!SpawnRules.Allows(area.AreaId))
        {
            call.Refuse(SpawnText.OutsideOnly);
            return;
        }

        var place = new SpawnPlace(main.Position, main.Rotation);
        var by = call.Sender;
        fire.Run(SetAction, async () =>
        {
            try
            {
                await spawn.SetAsync(place, by.Ref);
            }
            catch (Exception)
            {
                // The writer logged it.
                if (by.IsLive)
                    call.Refuse(SpawnText.SetFailed);

                return;
            }

            audit.Log(SetAction, by.Ref, null, new SpawnDetails(place.Position.X, place.Position.Y, place.Position.Z, place.Yaw));
            if (by.IsLive)
                call.Reply(SpawnText.Set);
        });
    }

    private void GoThere(CommandCall call)
    {
        if (spawn.Current is not { } place)
        {
            call.Refuse(SpawnText.None);
            return;
        }

        if (teleports.TryMove(call, call.Sender, GameSpace.Outside, place.Position, Destination))
            call.Reply(SpawnText.Went);
    }

    private sealed record SpawnDetails(float X, float Y, float Z, float Yaw);
}
