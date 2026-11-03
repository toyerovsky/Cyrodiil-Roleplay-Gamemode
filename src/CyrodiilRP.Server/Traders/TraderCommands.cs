using CyrodiilRP.Data.Staff;
using CyrodiilRP.Server.Admin;
using CyrodiilRP.Server.Commands;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Interactions;
using CyrodiilRP.Server.Localization;
using OblivionMp.Sdk.Common.Archetypes;
using OblivionMp.Sdk.Common.Archetypes.Mixins;
using OblivionMp.Sdk.Serverside.Spawners;
using ReadyM.Relay.Common.Oblivion;
using ReadyM.Relay.Common.Oblivion.ECS.Values;
using ReadyM.SDK.Archetypes.Core;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Server.Entities;

namespace CyrodiilRP.Server.Traders;

[Service]
internal sealed partial class TraderCommands(
    CommandRegistry commands,
    IEntities entities,
    TraderSpots spots,
    TraderNpcs npcs,
    TraderShops shops,
    Audit audit,
    Fire fire)
{
    private const string PlaceAction = "trader.place";
    private const string RemoveAction = "trader.remove";
    private const string Here = "here";
    private const string Remove = "remove";
    private const string List = "list";
    private const string ShopSeparator = ", ";

    private void Start() => commands.AddStaff(StaffPermissions.Trader, TraderText.CmdTrader, Run, needsCharacter: true);

    private void Run(CommandCall call)
    {
        switch (call.Words.Count > 0 ? call.Words[0] : string.Empty)
        {
            case Here:
                Place(call);
                break;

            case Remove:
                Take(call);
                break;

            case List:
                ListSpots(call);
                break;

            default:
                call.Refuse(TraderText.CmdTrader);
                break;
        }
    }

    private void Place(CommandCall call)
    {
        var shop = call.Words.Count > 1 ? call.Words[1] : string.Empty;
        if (WareCatalog.Shipped.FindShop(shop) is null)
        {
            call.Refuse(TraderText.UnknownShop(shop, string.Join(ShopSeparator, WareCatalog.Shipped.Shops.Select(s => s.Name))));
            return;
        }

        if (!entities.TryLookup(call.Sender.Id, out MainCharacter main) || !entities.TryGetScope(main, out Area area))
        {
            call.Refuse(TraderText.NotInWorld);
            return;
        }

        // After a far teleport the game can leave the character's cell behind for a while: a trader made in that cell
        // would never get a body.
        var parent = main.As<ParentCell>();
        var position = main.Position;
        if (parent.Kind == ParentCellKind.Exterior)
        {
            var seen = OblivionCellIds.Exterior(parent.X, parent.Y);
            var here = GameSpace.ToExteriorCell(position);
            if (seen != here)
            {
                call.Refuse(TraderText.NotSettled(seen: seen.ToManaged(), here: here.ToManaged()));
                return;
            }
        }

        var cell = parent.ToFullCellId(area.AreaId);
        var yaw = main.Rotation;
        var by = call.Sender;
        fire.Run(PlaceAction, async () =>
        {
            TraderSpotRecord spot;
            try
            {
                spot = await spots.AddAsync(shop, cell.AreaId.ToManaged(), cell.CellId.ToManaged(), position, yaw, by.Ref);
            }
            catch (Exception)
            {
                // The writer logged it.
                if (by.IsLive)
                    call.Refuse(TraderText.PlaceFailed);

                return;
            }

            audit.Log(PlaceAction, by.Ref, null, new SpotDetails(spot.Id, shop, spot.Area, spot.Cell));
            if (by.IsLive)
                call.Reply(TraderText.Placed(shop, spot.Id));
        });
    }

    private void Take(CommandCall call)
    {
        if (shops.FindNearestInFront(call.Sender, InteractionMenu.Reach, InteractionMenu.ConeDegrees, out _) is not { } spot)
        {
            call.Refuse(TraderText.NoneInFront);
            return;
        }

        // Written first: a spot taken from memory but left in the database would come back after a restart.
        var by = call.Sender;
        fire.Run(RemoveAction, async () =>
        {
            bool removed;
            try
            {
                removed = await spots.RemoveAsync(spot);
            }
            catch (Exception)
            {
                // The writer logged it.
                if (by.IsLive)
                    call.Refuse(TraderText.RemoveFailed(spot.Id));

                return;
            }

            if (removed)
            {
                shops.CloseAt(spot);
                npcs.Remove(spot);
                audit.Log(RemoveAction, by.Ref, null, new SpotDetails(spot.Id, spot.Shop, spot.Area, spot.Cell));
            }

            if (by.IsLive)
                call.Reply(TraderText.Removed(spot.Id));
        });
    }

    private void ListSpots(CommandCall call)
    {
        if (spots.All.Count == 0)
        {
            call.Reply(TraderText.ListEmpty);
            return;
        }

        foreach (var spot in spots.All)
            call.Reply(TraderText.ListEntry(spot.Id, spot.Shop, spot.Area, spot.Home.CellId.ToManaged(), (int)spot.Position.X, (int)spot.Position.Y, (int)spot.Position.Z));
    }

    private sealed record SpotDetails(long Spot, string Shop, string Area, string Cell);
}
