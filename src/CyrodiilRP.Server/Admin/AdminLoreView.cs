using System.Text.Json.Serialization;
using CyrodiilRP.Server.Lore;

namespace CyrodiilRP.Server.Admin;

// A player's lore card for the staff: every field, the owner's notes too, under the character's real name. A button per
// field clears it (/alore <number> clear <field>).
internal sealed record AdminLoreView(
    [property: JsonPropertyName("number")] int Number,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("namePublic")] bool NamePublic,
    [property: JsonPropertyName("view")] LoreViewJson View);
