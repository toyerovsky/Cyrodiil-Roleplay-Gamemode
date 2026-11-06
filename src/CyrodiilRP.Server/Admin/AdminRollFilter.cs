namespace CyrodiilRP.Server.Admin;

// What the panel's dice tab asks for. Player: a part of a character's name, or null for everyone. Before: the id the next
// page starts below.
internal sealed record AdminRollFilter(string? Player, long? Before);
