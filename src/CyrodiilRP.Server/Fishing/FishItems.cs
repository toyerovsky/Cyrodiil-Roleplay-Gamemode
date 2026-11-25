using CyrodiilRP.Common.Fishing;
using CyrodiilRP.Server.Players;
using OblivionMP.SDK.Common.Items;
using OblivionMP.SDK.Serverside.Items;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Fishing;

// The rods, the baits and the fish as items every game knows: defined as the server starts, kept in the world's save
// with the items carried.
[Service]
internal sealed partial class FishItems(ICustomItems customItems, FishingStock stock)
{
    private void Start()
    {
        foreach (var rod in RodCatalog.Items)
            customItems.Define(rod.Key, CustomItem.CopyOf(RodCatalog.Base)
                .WithName(rod.Name)
                .WithValue(rod.Value)
                .WithWeight(rod.Rod.Weight)
                .WithMesh(FishingGear.MeshOf(rod.Rod.Tier)));

        foreach (var bait in BaitCatalog.All)
        {
            if (bait.CustomKey is { } key)
                customItems.Define(key, CustomItem.CopyOf(bait.Base).WithName(bait.Name).WithValue(bait.Value).WithWeight(bait.Weight));
        }

        foreach (var fish in FishCatalog.Items)
            customItems.Define(fish.Key, CustomItem.CopyOf(FishingRules.FishBase)
                .WithName(fish.Name)
                .WithValue(fish.Value)
                .WithWeight(fish.Fish.Weight)
                .WithMesh(FishingRules.FishMesh)
                .WithIconOf(FishingRules.FishIcon));
    }

    public bool CarriesRod(OnlinePlayer player) => stock.FindBestRod(player) is not null;

    public bool TryGive(OnlinePlayer player, FishItem fish) => stock.TryFind(fish.Key, out var item) && stock.TryGive(player, item, 1);

    public bool TryGive(OnlinePlayer player, FindKind find) => stock.TryGive(player, find.Item, 1);
}
