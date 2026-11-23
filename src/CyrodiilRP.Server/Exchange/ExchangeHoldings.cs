using CyrodiilRP.Server.Admin;
using OblivionMP.SDK.Common.Archetypes.Mixins;
using ReadyM.Relay.Common.Oblivion.ECS.Values;

namespace CyrodiilRP.Server.Exchange;

// What a player can trade. A worn copy has to come off first, and an item the mode cannot name is left out: one a player
// made in the game (a potion, an enchanted sword), whose form the server does not keep over a restart.
internal sealed record ExchangeHoldings(int Gold, IReadOnlyList<ExchangeItem> Items)
{
    public static ExchangeHoldings Empty { get; } = new(0, []);

    public static ExchangeHoldings Read<T>(T inventory, int gold, Func<NetworkFormId, KnownItem?> describe) where T : struct, IInventory
    {
        var counts = new Dictionary<ExchangeStack, int>();
        for (var i = 0; i < inventory.ItemsCount; i++)
        {
            var entry = inventory.GetItems(i);
            var stack = new ExchangeStack(entry.FormId, entry.Health, entry.Charge);
            if (entry.Worn == 0 && stack.Item != ExchangeStack.Gold.Item)
                counts[stack] = counts.GetValueOrDefault(stack) + entry.Count;
        }

        var items = new List<ExchangeItem>();
        foreach (var (stack, count) in counts)
        {
            if (describe(stack.Item) is { } item)
                items.Add(new ExchangeItem(stack, item, count));
        }

        return new ExchangeHoldings(gold, Sort(items));
    }

    // By name, and stacks of one name always in the same order, so a window does not jump.
    public static IReadOnlyList<ExchangeItem> Sort(IEnumerable<ExchangeItem> items) =>
        items.OrderBy(item => item.Item.Name, StringComparer.OrdinalIgnoreCase).ThenBy(item => item.Stack.Key, StringComparer.Ordinal).ToList();

    public ExchangeItem? Find(string key) => Items.FirstOrDefault(item => item.Stack.Key == key);

    public int Count(ExchangeStack stack) => Items.FirstOrDefault(item => item.Stack == stack)?.Count ?? 0;

    public bool Matches(ExchangeHoldings other) => Gold == other.Gold && Items.SequenceEqual(other.Items);
}
