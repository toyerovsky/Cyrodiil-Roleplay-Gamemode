namespace CyrodiilRP.Server.Lore;

// A player around. Id: their number. Relation: "stranger", "known" (both know each other), "knowsYou" or "youKnow".
// Incoming and Outgoing: "introduce" while an introduction waits between the two, otherwise null.
internal sealed record LoreNearbyPlayerJson(
    string Id,
    string Header,
    double Meters,
    string Relation,
    bool HasCard,
    string? Incoming,
    string? Outgoing);
