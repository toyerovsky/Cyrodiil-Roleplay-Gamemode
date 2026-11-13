using System.Numerics;
using Microsoft.Extensions.Logging;
using OblivionMP.SDK.Common.Archetypes;
using OblivionMP.SDK.Common.Archetypes.Mixins;
using OblivionMP.SDK.Serverside.Items;
using ReadyM.Api.Idents;
using ReadyM.Relay.Common.Oblivion.ECS.Values;
using ReadyM.Relay.Common.Oblivion.Items;
using ReadyM.Relay.Common.Oblivion.Rpc;
using ReadyM.Relay.Server.Sdk.Players;
using ReadyM.SDK.Archetypes.Core;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Entities;
using ReadyM.SDK.Server.Entities;

namespace Roleplay.Jobs.Server;

/// <summary>
/// The foremen's dealings, behind <see cref="IJobForemen"/>: the dock foreman pays the crates owed, the others sell
/// their tools for gold and buy what their jobs give. Gold is the game's gold item in the player's pack.
/// </summary>
/// <remarks>Server thread only.</remarks>
[Service]
public sealed partial class JobForemen(
    IEntities entities,
    IPlayers players,
    ForemanNpcs npcs,
    DocksStore store,
    WageStore wages,
    JobItemResolver items,
    JobsConfig config,
    ILogger logger) : IJobForemen
{
    private readonly Inventories _inventories = new();

    private DocksConfig Settings => config.Docks;

    public float? MeasureInFront(PlayerId player, float reach, float coneDegrees)
        => NearestInFront(player, reach, coneDegrees)?.Distance;

    public string? FindInFront(PlayerId player, float reach, float coneDegrees)
        => NearestInFront(player, reach, coneDegrees)?.Foreman.Id;

    public ForemanWindow? WindowOf(PlayerId player, string foremanId)
    {
        if (RoleOf(foremanId) is not { } role || !entities.TryLookup<MainCharacter, PlayerId>(player, out var character))
            return null;

        var inventory = EntityHandle.Of(character).As<Equipment>();
        var unpaid = role.PaysWages && players.GetReadyMId(player) is { } account ? wages.Unpaid(account) : 0;
        var carried = new Dictionary<string, int>();
        foreach (var ware in role.Buys)
            carried[ware.Name] = carried.GetValueOrDefault(ware.Name) + Count(inventory, ware.Item);
        return new ForemanWindow(foremanId, role.Name, Count(inventory, Items.Gold), role.PaysWages, unpaid, Settings.Wage,
            ForemanDeals.OffersFor(role, carried));
    }

    public float? DistanceTo(PlayerId player, string foremanId)
    {
        var foreman = store.Foremen.FirstOrDefault(other => other.Id == foremanId);
        if (foreman is null || !npcs.TryFind(foreman, out var npc) || !entities.TryLookup(player, out MainCharacter main)
            || !SameArea(main, npc))
            return null;
        return Vector3.Distance(main.Position, npc.Position);
    }

    public ForemanDealResult Deal(PlayerId player, string foremanId, string offerId, int count)
    {
        if (RoleOf(foremanId) is not { } role || DistanceTo(player, foremanId) is null)
            return new ForemanDealResult(false, "The foreman is no longer there.");
        if (!entities.TryLookup<MainCharacter, PlayerId>(player, out var character))
            return new ForemanDealResult(false, "You are not in the world.");
        if (offerId != ForemanDeals.PayId && count is < 1 or > ForemanDeals.MostPieces)
            return new ForemanDealResult(false, $"Deal in 1 to {ForemanDeals.MostPieces} at a time.");

        var inventory = EntityHandle.Of(character).As<Equipment>();
        if (offerId == ForemanDeals.PayId && role.PaysWages)
            return Pay(player, inventory);
        if (offerId.StartsWith(ForemanDeals.BuyPrefix, StringComparison.Ordinal)
            && int.TryParse(offerId[ForemanDeals.BuyPrefix.Length..], out var index) && index >= 0 && index < role.Sells.Count)
            return Buy(player, inventory, role.Sells[index], count);
        if (offerId.StartsWith(ForemanDeals.SellPrefix, StringComparison.Ordinal))
        {
            var name = offerId[ForemanDeals.SellPrefix.Length..];
            var bought = role.Buys.Where(ware => ware.Name == name).ToList();
            if (bought.Count > 0)
                return Sell(player, inventory, name, bought, count);
        }

        logger.LogWarning("Jobs: {Player} asked the {Role} for {Offer}, which they do not offer.", player, role.Name, offerId);
        return new ForemanDealResult(false, "The foreman does not deal in that.");
    }

    private ForemanRole? RoleOf(string foremanId)
    {
        var foreman = store.Foremen.FirstOrDefault(other => other.Id == foremanId);
        var index = foreman is null ? -1 : npcs.RoleIndexOf(foreman);
        return index < 0 ? null : Settings.Foremen[index];
    }

    private ForemanDealResult Pay(PlayerId player, Equipment inventory)
    {
        if (players.GetReadyMId(player) is not { } account)
            return new ForemanDealResult(false, "The foreman cannot tell who you are.");
        var crates = wages.TakeAll(account);
        if (crates == 0)
            return new ForemanDealResult(false, "You have delivered no crates since you were last paid.");
        var gold = crates * Settings.Wage;
        _inventories.Add(inventory, new InventoryEntry(Items.Gold, gold, 0));
        logger.LogInformation("Jobs: {Player} was paid {Gold} gold for {Crates} crates.", player, gold, crates);
        return new ForemanDealResult(true, $"The foreman pays you {gold} gold for {ForemanDeals.Pieces(crates, crates == 1 ? "crate" : "crates")}.");
    }

    private ForemanDealResult Buy(PlayerId player, Equipment inventory, ForemanWare ware, int count)
    {
        if (!items.TryResolve(ware.Item, out var item))
            return new ForemanDealResult(false, $"The foreman has no {ware.Name} yet. Try again in a moment.");
        var price = ware.Price * count;
        if (!_inventories.TryTake(inventory, Stack(Items.Gold, price)))
            return new ForemanDealResult(false, $"That is {price} gold, and you carry {Count(inventory, Items.Gold)}.");
        _inventories.Add(inventory, new InventoryEntry(item, count, 0));
        logger.LogInformation("Jobs: {Player} bought {Count} {Item} for {Price} gold.", player, count, ware.Name, price);
        return new ForemanDealResult(true, $"You buy {ForemanDeals.Pieces(count, ware.Name)} for {price} gold.");
    }

    private ForemanDealResult Sell(PlayerId player, Equipment inventory, string name, IReadOnlyList<ForemanWare> bought, int count)
    {
        var sold = 0;
        var gold = 0;
        foreach (var ware in bought)
        {
            if (sold == count || !items.TryResolve(ware.Item, out var item))
                continue; // a custom item not made yet: nobody carries one
            var taking = Math.Min(count - sold, _inventories.CountCarried(inventory, Stack(item, 1)));
            if (taking == 0 || !_inventories.TryTake(inventory, Stack(item, taking)))
                continue;
            sold += taking;
            gold += taking * ware.Price;
        }
        if (sold == 0)
            return new ForemanDealResult(false, $"You carry no {name}.");
        _inventories.Add(inventory, new InventoryEntry(Items.Gold, gold, 0));
        logger.LogInformation("Jobs: {Player} sold {Count} {Item} for {Gold} gold.", player, sold, name, gold);
        return new ForemanDealResult(true, $"You sell {ForemanDeals.Pieces(sold, name)} for {gold} gold.");
    }

    private int Count(Equipment inventory, string itemText)
        => items.TryResolve(itemText, out var item) ? Count(inventory, item) : 0;

    private int Count(Equipment inventory, NetworkFormId item) => _inventories.CountCarried(inventory, Stack(item, 1));

    private static ItemStackData Stack(NetworkFormId item, int count) => new()
    {
        BaseId = item,
        Count = count,
        Durability = InventoryEntry.NoInstanceOverride,
        Charge = InventoryEntry.NoInstanceOverride,
    };

    private bool SameArea(MainCharacter main, Npc npc)
        => entities.TryGetScope(main, out Area area) && entities.TryGetScope(EntityHandle.Of(npc), out Area npcArea) && npcArea.AreaId == area.AreaId;

    private sealed record Found(Foreman Foreman, float Distance);

    // The nearest foreman standing in front of the player, in their area
    private Found? NearestInFront(PlayerId player, float reach, float coneDegrees)
    {
        if (!entities.TryLookup(player, out MainCharacter main))
            return null;

        Found? nearest = null;
        foreach (var foreman in store.Foremen)
        {
            if (npcs.RoleIndexOf(foreman) < 0 || !npcs.TryFind(foreman, out var npc) || !SameArea(main, npc))
                continue;
            var point = npc.Position;
            if (!InCone(main.Position, main.Rotation, point, reach, coneDegrees))
                continue;
            var distance = Vector3.Distance(main.Position, point);
            if (nearest is null || distance < nearest.Distance)
                nearest = new Found(foreman, distance);
        }
        return nearest;
    }

    private static bool InCone(Vector3 origin, float yawDegrees, Vector3 point, float reach, float coneDegrees)
    {
        var offset = point - origin;
        if (offset.Length() > reach)
            return false;
        var flat = new Vector2(offset.X, offset.Y);
        if (flat.Length() < 1f)
            return true;
        var yaw = yawDegrees * MathF.PI / 180f;
        return Vector2.Dot(Vector2.Normalize(flat), new Vector2(MathF.Cos(yaw), MathF.Sin(yaw))) >= MathF.Cos(coneDegrees / 2f * MathF.PI / 180f);
    }
}
