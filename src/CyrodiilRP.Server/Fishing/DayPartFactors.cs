namespace CyrodiilRP.Server.Fishing;

// How much likelier a species bites at each part of the day: 1 as usual.
internal sealed record DayPartFactors(double Dawn, double Day, double Dusk, double Night)
{
    public double Of(DayPart hour) => hour switch
    {
        DayPart.Dawn => Dawn,
        DayPart.Day => Day,
        DayPart.Dusk => Dusk,
        DayPart.Night => Night,
    };
}
