namespace CyrodiilRP.Server.Fishing;

// A line of waters.json: a named water, its kind, the height of its surface and its outline seen from above, each point
// x and y in the server's centimetres.
internal sealed record WaterEntry(string Id, string Name, WaterKind Kind, float SurfaceZ, float[][] Polygon);
