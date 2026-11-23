using CyrodiilRP.Server.Admin;
using CyrodiilRP.Server.Economy;
using OblivionMP.SDK.Common.Archetypes;
using OblivionMP.SDK.Common.Archetypes.Mixins;
using OblivionMP.SDK.Serverside.Items;
using ReadyM.Api.Idents;
using ReadyM.Relay.Common.Oblivion.ECS.Values;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Entities;
using ReadyM.SDK.Server.Entities;

namespace CyrodiilRP.Server.Exchange;

[Service]
internal sealed partial class ExchangeInventory(IEntities entities, Inventories inventories, Wallet wallet, KnownItems known)
{
    // Nothing while the character is not in the world.
    public ExchangeHoldings Read(PlayerId player) => Read(player, known.Describe);

    // Only what describe names.
    public ExchangeHoldings Read(PlayerId player, Func<NetworkFormId, KnownItem?> describe) =>
        entities.TryLookup(player, out MainCharacter main) && wallet.TryCount(player, out var gold)
            ? ExchangeHoldings.Read(GetInventory(main), gold, describe)
            : ExchangeHoldings.Empty;

    public bool TryMove(ExchangeMove move) =>
        entities.TryLookup(move.From.Id, out MainCharacter giver)
        && entities.TryLookup(move.To.Id, out MainCharacter taker)
        && inventories.TryMove(GetInventory(giver), GetInventory(taker), move.Stack.CreateData(move.Count));

    private static Equipment GetInventory(in MainCharacter main) => EntityHandle.Of(main).As<Equipment>();
}
