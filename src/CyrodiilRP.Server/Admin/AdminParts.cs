namespace CyrodiilRP.Server.Admin;

// What the panel asks for apart from its state, and what comes back under the same name.
internal static class AdminParts
{
    // A player's card, by number.
    public const string Card = "card";

    // Every ban, mute and freeze in force.
    public const string Sanctions = "sanctions";

    // A page of the audit, by an AdminAuditFilter as JSON.
    public const string Audit = "audit";

    // The game's items whose name or editor id holds the text, of a type or of any (an AdminItemFilter as JSON).
    public const string Items = "items";

    // A page of the roll log, by an AdminRollFilter as JSON.
    public const string Rolls = "rolls";

    // A player's lore card in full, every field, by number (an AdminLoreView).
    public const string Lore = "lore";
}
