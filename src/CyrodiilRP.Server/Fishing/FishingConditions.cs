namespace CyrodiilRP.Server.Fishing;

// Where and when a line goes in: what decides the species that bite.
internal sealed record FishingConditions(WaterKind Water, DayPart Hour, WeatherKind Weather);
