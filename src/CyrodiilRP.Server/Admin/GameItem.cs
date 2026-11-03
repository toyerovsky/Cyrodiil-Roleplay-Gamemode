namespace CyrodiilRP.Server.Admin;

// Id: the game's editor id. Type: its record's ("WEAP", "CLOT", ...).
internal sealed record GameItem(string Id, uint Form, string Name, string Type);
