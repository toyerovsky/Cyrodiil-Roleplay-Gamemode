namespace CyrodiilRP.Data.Lore;

// A character's lore card as stored: whether its name is public, and every field with a row.
public sealed record LoreCardRecord(bool NamePublic, IReadOnlyList<LoreField> Fields);
