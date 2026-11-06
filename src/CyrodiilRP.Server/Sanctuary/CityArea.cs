namespace CyrodiilRP.Server.Sanctuary;

// One area of the city: its OB:MP area id, the game's editor id and the name players see.
internal readonly record struct CityArea(string Id, string EditorId, string Name);
