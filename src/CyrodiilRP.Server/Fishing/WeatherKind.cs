namespace CyrodiilRP.Server.Fishing;

// The area's weather, by the plain weathers of Admin/weathers.json.
internal enum WeatherKind
{
    Clear,
    Cloudy,
    Overcast,
    Fog,
    Rain,
    Thunderstorm,
    Snow,

    // A weather of the game's the list does not name (a region's own, Oblivion's), or none yet: it changes nothing.
    Other,
}
