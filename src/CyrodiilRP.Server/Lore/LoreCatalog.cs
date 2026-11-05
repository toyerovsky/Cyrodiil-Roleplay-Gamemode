using System.Text.Json;
using CyrodiilRP.Server.Admin;

namespace CyrodiilRP.Server.Lore;

// MESOL's lore list, shipped in the assembly. It is the mode's own data: an entry that does not read is a mistake in the
// file, so loading throws.
internal sealed class LoreCatalog
{
    private const string Resource = "mesol-lore.json";

    private readonly Dictionary<string, LoreEntry> _byId;

    private LoreCatalog(Dictionary<string, LoreEntry> byId) => _byId = byId;

    public static LoreCatalog Shipped { get; } = Load(EmbeddedData.Read(Resource));

    public int Count => _byId.Count;

    public IEnumerable<LoreEntry> Entries => _byId.Values;

    public static LoreCatalog Load(string json)
    {
        var file = JsonSerializer.Deserialize(json, LoreJson.Default.LoreCatalogFile)
                   ?? throw new FormatException("The lore list is empty.");
        var byId = new Dictionary<string, LoreEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in file.Entries)
        {
            if (!byId.TryAdd(entry.Id, Read(entry)))
                throw new FormatException($"The lore list has {entry.Id} twice.");
        }

        return new LoreCatalog(byId);
    }

    public bool TryGet(string? id, out LoreEntry entry)
    {
        if (id is not null && _byId.TryGetValue(id, out var found))
        {
            entry = found;
            return true;
        }

        entry = null!;
        return false;
    }

    // An id the list no longer has (a card written before an entry went) shows as it is.
    public string NameOf(string id) => TryGet(id, out var entry) ? entry.Name : id;

    // The exact name first, then a name starting with the query, then the Remaster's own, then by name.
    public List<LoreEntry> Search(IReadOnlyCollection<LoreKind> kinds, string query, Func<LoreEntry, bool> allowed, int max)
    {
        var wanted = query.Trim();
        return _byId.Values
            .Where(e => kinds.Contains(e.Kind) && allowed(e))
            .Where(e => wanted.Length == 0
                        || e.Name.Contains(wanted, StringComparison.OrdinalIgnoreCase)
                        || e.Id.Contains(wanted, StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => string.Equals(e.Name, wanted, StringComparison.OrdinalIgnoreCase) ? 0
                : e.Name.StartsWith(wanted, StringComparison.OrdinalIgnoreCase) ? 1 : 2)
            .ThenBy(e => e.RemasteredOrigin ? 0 : 1)
            .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .Take(max)
            .ToList();
    }

    private static LoreEntry Read(LoreCatalogFileEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Id) || string.IsNullOrWhiteSpace(entry.Name))
            throw new FormatException($"A lore entry has no id or name ({entry.Id}).");

        if (!Enum.TryParse<LoreKind>(entry.Kind, ignoreCase: true, out var kind) || !Enum.IsDefined(kind))
            throw new FormatException($"The lore entry {entry.Id} is of the kind {entry.Kind}, which is none.");

        var era = entry.Era switch
        {
            "current" => LoreEra.Current,
            "historical" => LoreEra.Historical,
            "future" => LoreEra.Future,
            _ => throw new FormatException($"The lore entry {entry.Id} is of the era {entry.Era}, which is none."),
        };
        var fit = entry.ModelFit switch
        {
            null or "n/a" => LoreModelFit.NotApplicable,
            "plausible" => LoreModelFit.Plausible,
            "implausible" => LoreModelFit.Implausible,
            _ => throw new FormatException($"The lore entry {entry.Id} has the model fit {entry.ModelFit}, which is none."),
        };
        return new LoreEntry(entry.Id, entry.Race, kind, entry.Name, entry.Parent, entry.Summary, era, fit, entry.RemasteredOrigin);
    }
}
