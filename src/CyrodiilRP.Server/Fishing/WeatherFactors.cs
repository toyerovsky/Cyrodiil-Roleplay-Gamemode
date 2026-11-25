namespace CyrodiilRP.Server.Fishing;

// How much likelier a species bites in each weather: 1 as usual. A weather the list does not name changes nothing.
internal sealed record WeatherFactors(double Clear, double Cloudy, double Overcast, double Fog, double Rain, double Thunderstorm, double Snow)
{
    public static readonly WeatherFactors Even = new(Clear: 1, Cloudy: 1, Overcast: 1, Fog: 1, Rain: 1, Thunderstorm: 1, Snow: 1);

    public double Of(WeatherKind weather) => weather switch
    {
        WeatherKind.Clear => Clear,
        WeatherKind.Cloudy => Cloudy,
        WeatherKind.Overcast => Overcast,
        WeatherKind.Fog => Fog,
        WeatherKind.Rain => Rain,
        WeatherKind.Thunderstorm => Thunderstorm,
        WeatherKind.Snow => Snow,
        WeatherKind.Other => 1,
    };
}
