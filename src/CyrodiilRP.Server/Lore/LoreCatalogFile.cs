using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Lore;

internal sealed record LoreCatalogFile([property: JsonPropertyName("entries")] IReadOnlyList<LoreCatalogFileEntry> Entries);
