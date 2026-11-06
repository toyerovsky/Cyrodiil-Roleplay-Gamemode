using CyrodiilRP.Server.Lore;

namespace CyrodiilRP.Server.Admin;

// A player's lore card for the staff: every field, the owner's notes too, under the character's real name. A button per
// field clears it (/alore <number> clear <field>).
internal sealed record AdminLoreView(int Number, string Name, bool NamePublic, LoreViewJson View);
