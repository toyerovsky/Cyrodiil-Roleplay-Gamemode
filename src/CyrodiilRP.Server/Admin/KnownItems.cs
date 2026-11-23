using OblivionMP.SDK.Serverside.Items;
using ReadyM.Relay.Common.Oblivion.ECS.Values;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Admin;

// The items the mode can name: the game's (items.json) and the custom items the mods define (the SDK's ICustomItems). A
// custom item's form id alone is 0, so an item is named by its whole NetworkFormId.
[Service]
internal sealed partial class KnownItems(ICustomItems customItems)
{
    public const string CustomPrefix = "custom:";

    public KnownItem? Describe(NetworkFormId item) => Describe(item, ItemCatalog.Shipped, customItems);

    // What inventories name a custom item kept by its key by now; false when no mod defines the key in this run.
    public bool TryFindCustom(string key, out NetworkFormId item) => customItems.TryGet(key, out item);

    // Null for a game item.
    public static string? CustomKeyOf(KnownItem item) =>
        item.Id.StartsWith(CustomPrefix, StringComparison.Ordinal) ? item.Id[CustomPrefix.Length..] : null;

    public Found<KnownItem> Find(string text) => Find(text, ItemCatalog.Shipped, customItems);

    public IReadOnlyList<KnownItem> Search(string? text, string? type, int most) => Search(text, type, most, ItemCatalog.Shipped, customItems);

    public static KnownItem? Describe(NetworkFormId item, ItemCatalog catalog, ICustomItems customItems) =>
        customItems.TryFind(item, out var custom) ? Of(custom, catalog) : DescribeGameItem(item, catalog);

    // Null for a custom item too: its form id is 0, which names no game item.
    public static KnownItem? DescribeGameItem(NetworkFormId item, ItemCatalog catalog) =>
        catalog.FindByForm(item.FormId.Value) is { } game ? KnownItem.Of(game) : null;

    public static Found<KnownItem> Find(string text, ItemCatalog catalog, ICustomItems customItems) =>
        CatalogLookup.Find([.. ListCustom(catalog, customItems), .. catalog.All.Select(KnownItem.Of)], text, item => item.Id, item => item.Name);

    // The custom items first, as the game's would bury them, then the game's as the catalogue searches them.
    public static IReadOnlyList<KnownItem> Search(string? text, string? type, int most, ItemCatalog catalog, ICustomItems customItems)
    {
        var wanted = text?.Trim() ?? string.Empty;
        var ofType = string.IsNullOrWhiteSpace(type) ? null : type.Trim();
        if (wanted.Length == 0 && ofType is null)
            return [];

        var custom = ListCustom(catalog, customItems);
        var found = wanted.Length == 0 ? custom : CatalogLookup.FindContaining(custom, wanted, item => item.Id, item => item.Name);
        return found
            .Where(item => ofType is null || item.Type.Equals(ofType, StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Concat(catalog.Search(text, type, most).Select(KnownItem.Of))
            .Take(most)
            .ToList();
    }

    private static List<KnownItem> ListCustom(ItemCatalog catalog, ICustomItems customItems) =>
        customItems.ListDefined().Select(custom => Of(custom, catalog)).ToList();

    private static KnownItem Of(DefinedCustomItem custom, ItemCatalog catalog) =>
        new(CustomPrefix + custom.Key, custom.Id, custom.Item.Name, catalog.FindByForm(custom.Item.Base.Value)?.Type ?? string.Empty);
}
