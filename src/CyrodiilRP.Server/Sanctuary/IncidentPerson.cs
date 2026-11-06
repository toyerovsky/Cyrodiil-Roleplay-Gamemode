namespace CyrodiilRP.Server.Sanctuary;

// A player as an incident names them: the ReadyM account and the real name at the time.
internal sealed record IncidentPerson(Guid Account, string Name);
