using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using CyrodiilRP.Server.Commands;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Players;
using OblivionMp.Sdk.Common.Archetypes;
using ReadyM.Api.Idents;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Server.Entities;

namespace CyrodiilRP.Server.Admin;

[Service]
internal sealed partial class Teleports(IEntities entities, Proximity proximity, OnlinePlayers online, Audit audit)
{
    private const string TeleportAction = "admin.teleport";

    // Above the spot rather than in the ground under it: the fall is short and the game settles the character.
    public static readonly Vector3 Lift = new(0f, 0f, 100f);

    private readonly Dictionary<PlayerId, SavedSpot> _before = [];

    private void Start() => online.Left += player => _before.Remove(player.Id);

    public bool TryLocate(CommandCall call, OnlinePlayer player, out SavedSpot spot)
    {
        if (proximity.TryLocate(player.Id, out var area, out var position))
        {
            spot = new SavedSpot(area, position);
            return true;
        }

        spot = new SavedSpot(default, default);
        call.Refuse(AdminText.NotInWorld(player.Name));
        return false;
    }

    // A little above the spot. The server moves a character only within the area it stands in. Destination: what the
    // audit says it went to.
    public bool TryMove(CommandCall call, OnlinePlayer moved, AreaId area, Vector3 position, string destination) =>
        TryPut(call, moved, area, position + Lift, destination);

    // To the very point, with no lift: for a height the staff member measured themselves.
    public bool TryPut(CommandCall call, OnlinePlayer moved, AreaId area, Vector3 position, string destination)
    {
        if (!TryLocate(call, moved, out var from) || !entities.TryLookup(moved.Id, out MainCharacter main))
            return false;

        if (from.Area != area)
        {
            call.Refuse(AdminText.OtherArea(name: moved.Name, area: from.Area.ToString()));
            return false;
        }

        _before[moved.Id] = from;
        main.Position = position;
        audit.Log(TeleportAction, call.Sender.Ref, moved.Ref, new TeleportDetails(destination));
        return true;
    }

    public bool TryBefore(OnlinePlayer player, [NotNullWhen(true)] out SavedSpot? spot) => _before.TryGetValue(player.Id, out spot);

    private sealed record TeleportDetails(string To);
}
