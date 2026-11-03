using OblivionMp.Sdk.Common.Archetypes.Mixins;
using ReadyM.Relay.Common.Oblivion.ECS.Values;

namespace CyrodiilRP.Tests.Fakes;

// An item list without the world.
internal readonly struct FakeInventory(List<InventoryEntry> items) : IInventory
{
    public int ItemsCount => items.Count;

    public InventoryEntry GetItems(int index) => items[index];

    public void AddItems(in InventoryEntry value) => items.Add(value);

    public void InsertItems(int index, in InventoryEntry value) => items.Insert(index, value);

    public InventoryEntry RemoveAtItems(int index)
    {
        var removed = items[index];
        items.RemoveAt(index);
        return removed;
    }
}
