using CyrodiilRP.Server.Admin;

namespace CyrodiilRP.Server.Fishing;

// The area's weather form as the fish feel it: one of the plain weathers the staff set, or Other.
internal static class FishingWeathers
{
    public static WeatherKind Of(uint weatherForm) => WeatherCatalog.Shipped.FindName(weatherForm) is { } name ? Parse(name) : WeatherKind.Other;

    public static WeatherKind Parse(string name) =>
        Enum.TryParse<WeatherKind>(name, ignoreCase: true, out var kind) && kind != WeatherKind.Other
            ? kind
            : throw new InvalidOperationException($"The weather '{name}' of weathers.json is no weather the fishing knows.");
}
