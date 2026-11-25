namespace CyrodiilRP.Server.Fishing;

// The world's time of day as the fish keep it, in minutes from midnight.
internal static class FishingHours
{
    private const float MinutesInHour = 60f;
    private const float MinutesInDay = 24 * MinutesInHour;

    public const float DawnFrom = 5 * MinutesInHour;
    public const float DayFrom = 8 * MinutesInHour;
    public const float DuskFrom = 18 * MinutesInHour;
    public const float NightFrom = 21 * MinutesInHour;

    public static DayPart Of(float timeOfDayMinutes)
    {
        if (!(timeOfDayMinutes >= 0f && timeOfDayMinutes < MinutesInDay))
            throw new ArgumentOutOfRangeException(nameof(timeOfDayMinutes), timeOfDayMinutes, "The time of day is minutes from midnight, under a day.");

        return timeOfDayMinutes switch
        {
            < DawnFrom => DayPart.Night,
            < DayFrom => DayPart.Dawn,
            < DuskFrom => DayPart.Day,
            < NightFrom => DayPart.Dusk,
            _ => DayPart.Night,
        };
    }
}
