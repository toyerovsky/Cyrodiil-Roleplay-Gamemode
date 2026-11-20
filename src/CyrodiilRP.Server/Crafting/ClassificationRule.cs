using System.Text.RegularExpressions;
using CyrodiilRP.Server.Admin;

namespace CyrodiilRP.Server.Crafting;

// A rule that asks for what its class has no use for, or leaves out what it needs, is a mistake in the file, so
// reading it throws.
internal sealed class ClassificationRule
{
    private const string KeyGroup = "key";

    private readonly string? _type;
    private readonly Regex? _id;
    private readonly ItemClass _class;
    private readonly Product? _product;

    private ClassificationRule(string name, string? type, Regex? id, ItemClass itemClass, Product? product)
    {
        Name = name;
        _type = type;
        _id = id;
        _class = itemClass;
        _product = product;
    }

    public string Name { get; }

    public string? Table => _product?.Table?.Name;

    public static ClassificationRule Read(ClassificationRuleEntry entry, int number, IReadOnlyDictionary<string, Dictionary<string, int>> tables)
    {
        var name = $"rule {number} ({entry.Type} {entry.Id})";
        if (entry.Type is null && entry.Id is null)
            throw new InvalidOperationException($"{name} names neither a type nor an id, so it would class every item.");

        var id = entry.Id is null ? null : new Regex(entry.Id, RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture);
        var product = entry.Class switch
        {
            ItemClass.Product => ReadProduct(entry, name, id, tables),
            ItemClass.Material or ItemClass.Relic or ItemClass.Exempt => RefuseProduct(entry, name),
        };

        return new ClassificationRule(name, entry.Type, id, entry.Class, product);
    }

    // Null when the rule does not fit the item.
    public ItemClassification? Classify(GameItem item)
    {
        if (_type is not null && !string.Equals(_type, item.Type, StringComparison.Ordinal))
            return null;

        var match = _id?.Match(item.Id);
        if (match is { Success: false })
            return null;

        return new ItemClassification(_class, _product is null ? null : new CraftProduct(_product.Trade, ReadLevel(_product, item, match), _product.NeedsPattern));
    }

    private int ReadLevel(Product product, GameItem item, Match? match)
    {
        if (product.Table is not { } table)
            return product.Level;

        var key = match?.Groups[KeyGroup].Value ?? string.Empty;
        return table.Levels.TryGetValue(key, out var level)
            ? level
            : throw new InvalidOperationException($"{item.Id} falls under {Name} with the key \"{key}\", which the table {table.Name} lacks.");
    }

    private static Product ReadProduct(ClassificationRuleEntry entry, string name, Regex? id, IReadOnlyDictionary<string, Dictionary<string, int>> tables)
    {
        if (entry.Trade is not { } trade)
            throw new InvalidOperationException($"{name} is a product without a trade.");

        if (entry.Level is { } level && entry.Table is null)
            return new Product(trade, CheckLevel(level, name), null, entry.Pattern);

        if (entry.Table is not { } tableName || entry.Level is not null)
            throw new InvalidOperationException($"{name} is a product, which takes either a level or a table.");

        if (!tables.TryGetValue(tableName, out var levels))
            throw new InvalidOperationException($"{name} names the table {tableName}, which the file lacks.");

        if (id is null || id.GroupNumberFromName(KeyGroup) < 0)
            throw new InvalidOperationException($"{name} names a table, so its id needs the group \"{KeyGroup}\".");

        foreach (var value in levels.Values)
            CheckLevel(value, $"the table {tableName}");

        return new Product(trade, 0, new LevelTable(tableName, levels), entry.Pattern);
    }

    private static Product? RefuseProduct(ClassificationRuleEntry entry, string name) =>
        entry.Trade is null && entry.Level is null && entry.Table is null && !entry.Pattern
            ? null
            : throw new InvalidOperationException($"{name} is not a product, so it takes no trade, level, table or pattern.");

    private static int CheckLevel(int level, string owner) =>
        level is >= 0 and <= TradeRules.MaxLevel
            ? level
            : throw new InvalidOperationException($"{owner} has the level {level}, outside 0 to {TradeRules.MaxLevel}.");

    private sealed record LevelTable(string Name, IReadOnlyDictionary<string, int> Levels);

    // Level counts when there is no table. With one, the level is the table's under the id's key.
    private sealed record Product(Trade Trade, int Level, LevelTable? Table, bool NeedsPattern);
}
