namespace CyrodiilRP.Server.Fishing;

// What a player's game answered when asked where the water is: a FishingVerdict's name, as it came, and for water the
// way the character faces it and the height of its surface.
internal sealed record FishingSpotReport(string Verdict, float Yaw, float WaterZ);
