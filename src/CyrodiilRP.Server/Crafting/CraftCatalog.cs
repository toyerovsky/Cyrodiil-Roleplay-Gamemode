using System.Text.Json;
using CyrodiilRP.Server.Admin;

namespace CyrodiilRP.Server.Crafting;

// What every item of the game is to the crafting, by classification.json. An item no rule classes, a rule that classes
// nothing and a table no rule names are mistakes in the file, so loading throws.
internal sealed class CraftCatalog
{
    private const string Resource = "classification.json";
    private const int MostNamed = 20;

    private readonly Dictionary<uint, ItemClassification> _byForm;

    private CraftCatalog(Dictionary<uint, ItemClassification> byForm) => _byForm = byForm;

    public static CraftCatalog Shipped { get; } = Load(EmbeddedData.Read(Resource), ItemCatalog.Shipped);

    // Null for a form the game's files lack, as a custom item's.
    public ItemClassification? Find(uint form) => _byForm.GetValueOrDefault(form);

    public static CraftCatalog Load(string json, ItemCatalog items)
    {
        var file = JsonSerializer.Deserialize(json, ClassificationJson.Default.ClassificationFile)
                   ?? throw new InvalidOperationException($"{Resource} is empty.");
        var rules = file.Rules.Select((entry, index) => ClassificationRule.Read(entry, index + 1, file.Tables)).ToList();

        var unnamed = file.Tables.Keys.Except(rules.Select(rule => rule.Table).OfType<string>(), StringComparer.Ordinal).ToList();
        if (unnamed.Count > 0)
            throw new InvalidOperationException($"{Resource} has the tables {string.Join(", ", unnamed)}, which no rule names.");

        var byForm = new Dictionary<uint, ItemClassification>();
        var used = new HashSet<ClassificationRule>();
        var unclassed = new List<string>();
        foreach (var item in items.All)
        {
            var found = false;
            foreach (var rule in rules)
            {
                if (rule.Classify(item) is not { } classification)
                    continue;

                byForm[item.Form] = classification;
                used.Add(rule);
                found = true;
                break;
            }

            if (!found)
                unclassed.Add($"{item.Type} {item.Id}");
        }

        if (unclassed.Count > 0)
            throw new InvalidOperationException($"{Resource} classes {unclassed.Count} of the game's items under no rule: {string.Join(", ", unclassed.Take(MostNamed))}.");

        var idle = rules.Where(rule => !used.Contains(rule)).Select(rule => rule.Name).ToList();
        if (idle.Count > 0)
            throw new InvalidOperationException($"{Resource} has rules that class no item: {string.Join(", ", idle.Take(MostNamed))}.");

        return new CraftCatalog(byForm);
    }
}
