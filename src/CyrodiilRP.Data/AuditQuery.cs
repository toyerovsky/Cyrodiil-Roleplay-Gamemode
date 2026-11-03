namespace CyrodiilRP.Data;

// Every filter is optional. Action matches the start of the action's name ("admin." for every staff action), Actor and
// Target a part of a nickname or a character's name, or a whole ReadyM id. To is not included. A page goes on below
// BeforeId, the last entry of the page before.
public sealed record AuditQuery(string? Action, string? Actor, string? Target, DateTimeOffset? From, DateTimeOffset? To, long? BeforeId, int Size);
