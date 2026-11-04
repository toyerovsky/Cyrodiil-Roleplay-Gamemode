using System.Globalization;
using System.Text.Json;

namespace CyrodiilRP.Server.Admin;

// The game's named items (items.json), for /aitem, the player's card and the trade: the SDK knows an item only by its
// form id.
internal sealed class ItemCatalog
{
    private readonly Dictionary<uint, GameItem> _byForm;

    private ItemCatalog(List<GameItem> items)
    {
        All = items;
        _byForm = items.ToDictionary(item => item.Form);
    }

    public static ItemCatalog Shipped { get; } = Load(EmbeddedData.Read(EmbeddedData.Items));

    public IReadOnlyList<GameItem> All { get; }

    public static ItemCatalog Load(string json) =>
        new((JsonSerializer.Deserialize(json, AdminDataJson.Default.ListItemEntry) ?? [])
            .Select(entry => new GameItem(entry.Id, uint.Parse(entry.Form, NumberStyles.HexNumber, CultureInfo.InvariantCulture), entry.Name, entry.Type))
            .ToList());

    public GameItem? FindByForm(uint form) => _byForm.GetValueOrDefault(form);

    public Found<GameItem> Find(string text) => CatalogLookup.Find(All, text, item => item.Id, item => item.Name);

    // The whole name or id first, then by name. A type alone lists that type, nothing alone lists nothing.
    public IReadOnlyList<GameItem> Search(string? text, string? type, int most)
    {
        var wanted = text?.Trim() ?? string.Empty;
        var ofType = string.IsNullOrWhiteSpace(type) ? null : type.Trim();
        if (wanted.Length == 0 && ofType is null)
            return [];

        var found = wanted.Length == 0 ? All : CatalogLookup.FindContaining(All, wanted, item => item.Id, item => item.Name);
        return found
            .Where(item => ofType is null || item.Type.Equals(ofType, StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => !item.Name.Equals(wanted, StringComparison.OrdinalIgnoreCase) && !item.Id.Equals(wanted, StringComparison.OrdinalIgnoreCase))
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Take(most)
            .ToList();
    }
}
