using System.Reflection;
using ReadyM.Relay.Common.Oblivion;
using Catalogue = ReadyM.Relay.Common.Oblivion.Items.Items;

namespace Roleplay.HungerAndThirst.Server;

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
