using System.Globalization;
using System.Numerics;
using CyrodiilRP.Data.Staff;
using CyrodiilRP.Server.Commands;
using CyrodiilRP.Server.Localization;
using OblivionMp.Sdk.Common.Archetypes;
using OblivionMp.Sdk.Common.Archetypes.Mixins;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Server.Entities;

namespace CyrodiilRP.Server.Admin;

[Service]
internal sealed partial class TeleportCommands(CommandRegistry commands, IEntities entities, Teleports teleports)
{
    private const int CoordinateWords = 3;
    private const string CandidateSeparator = ", ";
    private const string BackDestination = "back";

    private void Start()
    {
        commands.AddStaff(StaffPermissions.Position, AdminText.CmdPosition, ShowPosition, offDuty: true);
        commands.AddStaff(StaffPermissions.Teleport, AdminText.CmdTeleport, Teleport);
        commands.AddStaff(StaffPermissions.Back, AdminText.CmdBack, GoBack);
    }

    private void ShowPosition(CommandCall call)
    {
        var target = call.Sender;
        if (call.Words.Count > 0 && !call.TryTarget(0, out target))
            return;

        if (!teleports.TryLocate(call, target, out var spot) || !entities.TryLookup(target.Id, out MainCharacter main))
            return;

        var cell = main.As<ParentCell>();
        var game = GameSpace.ToGame(spot.Position);
        call.Reply(AdminText.Position(name: target.Name, area: spot.Area.ToString(), cell: $"{cell.Kind} {cell.X},{cell.Y}",
            position: FormatCoordinates(spot.Position), game: FormatCoordinates(game)));
    }

    private void Teleport(CommandCall call)
    {
        if (call.Words.Count == CoordinateWords && TryCoordinates(call, out var point))
        {
            if (teleports.TryLocate(call, call.Sender, out var here)
                && teleports.TryMove(call, call.Sender, here.Area, point, FormatCoordinates(point)))
                call.Reply(AdminText.TeleportedTo(FormatCoordinates(point)));
            return;
        }

        var found = PlaceCatalog.Shipped.Find(call.Arguments);
        if (found.One is not { } place)
        {
            call.Refuse(found.Candidates.Count == 0
                ? AdminText.NoPlace(call.Arguments)
                : AdminText.WhichPlace(string.Join(CandidateSeparator, found.Candidates.Select(p => p.Id))));
            return;
        }

        if (teleports.TryMove(call, call.Sender, GameSpace.Outside, place.Position, place.Id))
            call.Reply(AdminText.TeleportedTo(place.Name));
    }

    private void GoBack(CommandCall call)
    {
        var target = call.Sender;
        if (call.Words.Count > 0 && (!call.TryTarget(0, out target) || !call.MayPunish(target, self: true)))
            return;

        if (!teleports.TryBefore(target, out var spot))
        {
            call.Refuse(AdminText.NoTeleportYet(target.Name));
            return;
        }

        if (teleports.TryMove(call, target, spot.Area, spot.Position, BackDestination))
            call.Reply(AdminText.TakenBack(target.Name));
    }

    // Three whole numbers, centimetres as /apos says them.
    private static bool TryCoordinates(CommandCall call, out Vector3 point)
    {
        point = default;
        if (!CommandLine.TryNumber(call.Words[0], out var x) || !CommandLine.TryNumber(call.Words[1], out var y)
            || !CommandLine.TryNumber(call.Words[2], out var z))
            return false;

        point = new Vector3(x, y, z);
        return true;
    }

    private static string FormatCoordinates(Vector3 point) =>
        string.Create(CultureInfo.InvariantCulture, $"{MathF.Round(point.X)} {MathF.Round(point.Y)} {MathF.Round(point.Z)}");
}
