using System.Globalization;
using System.Reflection;
using Microsoft.Extensions.Logging;
using ReadyM.Relay.Common.Oblivion;
using ReadyM.SDK.Attributes;
using Roleplay.HungerAndThirst.Common;
using Catalogue = ReadyM.Relay.Common.Oblivion.Items.Items;

namespace Roleplay.HungerAndThirst.Server;

/// <summary>
/// What every edible item gives on this server: <see cref="FoodCatalog.Defaults"/> with the changes from config.json
/// applied. Built once, on first use. A bad line in config.json is logged and skipped, so one typo doesn't take the
/// whole table down.
/// </summary>
[Service]
public sealed partial class FoodTable(SustenanceConfig config, ILogger logger)
{
    private IReadOnlyDictionary<uint, FoodItem>? _table;

    public IReadOnlyCollection<FoodItem> All => Table.Values.ToArray();

    public bool TryGet(uint formId, out FoodItem food) => Table.TryGetValue(formId, out food!);

    private IReadOnlyDictionary<uint, FoodItem> Table => _table ??= Load();

    private IReadOnlyDictionary<uint, FoodItem> Load()
    {
        List<string> problems = [];
        Dictionary<uint, FoodItem> table = Build(FoodCatalog.Defaults, config, ItemCatalogue.Find, problems);
        foreach (string problem in problems)
            logger.LogWarning("HungerAndThirst: config.json foods: {Problem}", problem);
        logger.LogInformation("HungerAndThirst: {Count} foods and drinks known.", table.Count);
        return table;
    }

    /// <summary>
    /// The defaults (unless <see cref="SustenanceConfig.UseDefaultFoods"/> is off) with each of
    /// <see cref="SustenanceConfig.Foods"/> applied in order. <paramref name="find"/> turns an editor id into a form id.
    /// Lines that can't be used are described in <paramref name="problems"/> and left out. Keyed by form id.
    /// </summary>
    public static Dictionary<uint, FoodItem> Build(IEnumerable<FoodItem> defaults, SustenanceConfig config,
        Func<string, FormId?> find, ICollection<string> problems)
    {
        Dictionary<uint, FoodItem> table = config.UseDefaultFoods
            ? defaults.ToDictionary(food => food.FormId.Value)
            : [];

        foreach (FoodEntry entry in config.Foods)
        {
            string item = entry.Item.Trim();
            if ((TryParseHex(item) ?? find(item)) is not { } formId)
            {
                problems.Add($"'{entry.Item}' is not an item. Use an editor id like \"Apple\" or a form id like \"0x00033680\".");
                continue;
            }

            if (entry.Remove)
            {
                table.Remove(formId.Value);
                continue;
            }
            if (entry.Satiety is < 0 or > SustenanceScale.Max || entry.Hydration is < 0 or > SustenanceScale.Max
                || entry.Satiety + entry.Hydration == 0)
            {
                problems.Add($"'{entry.Item}' needs satiety and hydration from 0 to {SustenanceScale.Max}, and at least one above 0.");
                continue;
            }

            table.TryGetValue(formId.Value, out FoodItem? existing);
            string key = existing?.Key ?? item;
            string name = string.IsNullOrWhiteSpace(entry.Name) ? existing?.Name ?? key : entry.Name.Trim();
            table[formId.Value] = new FoodItem(key, formId, name, entry.Satiety, entry.Hydration);
        }
        return table;
    }

    private static FormId? TryParseHex(string text)
    {
        if (!text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return null;
        return uint.TryParse(text.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint value)
            ? new FormId(value)
            : null;
    }
}

/// <summary>The SDK's item catalogue (<c>Items.Ingredients.Apple</c> and so on) looked up by field name.</summary>
public static class ItemCatalogue
{
    private static readonly Lazy<Dictionary<string, FormId>> ByName = new(() =>
    {
        Dictionary<string, FormId> byName = new(StringComparer.OrdinalIgnoreCase);
        foreach (Type category in typeof(Catalogue).GetNestedTypes().Prepend(typeof(Catalogue)))
            foreach (FieldInfo field in category.GetFields(BindingFlags.Public | BindingFlags.Static))
                if (field.FieldType == typeof(FormId) && field.GetValue(null) is FormId id)
                    byName.TryAdd(field.Name, id);
        return byName;
    });

    public static FormId? Find(string editorId) => ByName.Value.TryGetValue(editorId, out FormId id) ? id : null;
}
