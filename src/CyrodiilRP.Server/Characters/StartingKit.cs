using OblivionMP.SDK.Common.Archetypes.Mixins;
using ReadyM.Relay.Common.Oblivion;
using ReadyM.Relay.Common.Oblivion.Items;

namespace CyrodiilRP.Server.Characters;

internal static class StartingKit
{
    // Coarse Linen Shirt, Coarse Linens and Oiled Linen Shoes, the commoners' clothes of Oblivion.esm.
    public static readonly IReadOnlyList<FormId> Clothes =
        [Items.Clothes.LowerShirt04, Items.Clothes.LowerPants04, Items.Clothes.LowerShoes04];

    public static void Dress<T>(T inventory) where T : struct, IInventory
    {
        for (var i = inventory.ItemsCount - 1; i >= 0; i--)
            inventory.RemoveAtItems(i);

        foreach (var piece in Clothes)
            inventory.AddItems(WornClothes.CreateEntry(piece));
    }
}
