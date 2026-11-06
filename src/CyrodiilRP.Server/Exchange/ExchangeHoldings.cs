using CyrodiilRP.Server.Admin;
using OblivionMP.SDK.Common.Archetypes.Mixins;

namespace CyrodiilRP.Server.Exchange;

// What a player can trade. A worn copy has to come off first, and an item a player made is in no catalogue: the server
// does not keep its form over a restart.
internal sealed record ExchangeHoldings(int Gold, IReadOnlyList<ExchangeItem> Items)
{
    public static ExchangeHoldings Empty { get; } = new(0, []);

    public static ExchangeHoldings Read<T>(T inventory, int gold, ItemCatalog catalog) where T : struct, IInventory
    {
        var counts = new Dictionary<ExchangeStack, int>();
        for (var i = 0; i < inventory.ItemsCount; i++)
        {
            var entry = inventory.GetItems(i);
            var stack = new ExchangeStack(entry.FormId.FormId.Value, entry.Health, entry.Charge);
            if (entry.Worn == 0 && stack.Form != ExchangeStack.Gold.Form)
                counts[stack] = counts.GetValueOrDefault(stack) + entry.Count;
        }

        var items = new List<ExchangeItem>();
        foreach (var (stack, count) in counts)
        {
            if (catalog.FindByForm(stack.Form) is { } item)
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
